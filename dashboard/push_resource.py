import argparse
import asyncio
import base64
import json
import os
import sys

import websockets

HERE = os.path.dirname(os.path.abspath(__file__))
CARDS = os.path.join(HERE, "nina-telemetry-cards.js")
MARKER = "nina-telemetry-cards loaded"
PREFIX = "data:text/javascript;base64,"


def websocket_url(base):
    base = base.rstrip("/")
    if base.startswith("https://"):
        base = "wss://" + base[len("https://"):]
    elif base.startswith("http://"):
        base = "ws://" + base[len("http://"):]
    return base + "/api/websocket"


def is_ours(resource):
    url = resource.get("url", "")
    if not url.startswith(PREFIX):
        return False
    try:
        return MARKER in base64.b64decode(url[len(PREFIX):]).decode("utf-8", "replace")
    except ValueError:
        return False


async def push(base_url, token, dry_run):
    js = open(CARDS, "rb").read()
    if MARKER.encode() not in js:
        sys.exit(f"marker {MARKER!r} missing from {CARDS}")
    data_url = PREFIX + base64.b64encode(js).decode()

    async with websockets.connect(websocket_url(base_url), max_size=2**26) as ws:
        await ws.recv()
        await ws.send(json.dumps({"type": "auth", "access_token": token}))
        if json.loads(await ws.recv())["type"] != "auth_ok":
            sys.exit("authentication failed")
        next_id = 0

        async def call(message):
            nonlocal next_id
            next_id += 1
            await ws.send(json.dumps({**message, "id": next_id}))
            while True:
                reply = json.loads(await ws.recv())
                if reply.get("id") == next_id and reply.get("type") == "result":
                    if not reply["success"]:
                        sys.exit(f"{message['type']} failed: {reply.get('error')}")
                    return reply["result"]

        existing = [r for r in await call({"type": "lovelace/resources"}) if is_ours(r)]
        print(f"cards {len(js)} B; existing resources: {[r['id'] for r in existing]}")
        if dry_run:
            return
        if not existing:
            created = await call({"type": "lovelace/resources/create", "res_type": "module", "url": data_url})
            print("created", created["id"])
            return
        first, *duplicates = existing
        if first["url"] == data_url:
            print("unchanged")
        else:
            await call({"type": "lovelace/resources/update", "resource_id": first["id"], "res_type": "module", "url": data_url})
            print("updated", first["id"])
        for duplicate in duplicates:
            await call({"type": "lovelace/resources/delete", "resource_id": duplicate["id"]})
            print("deleted duplicate", duplicate["id"])


def main():
    parser = argparse.ArgumentParser(
        description="Install nina-telemetry-cards.js as a Lovelace module resource (inlined as a data: URL). "
                    "Reads HA_URL (e.g. http://homeassistant.local:8123) and HA_TOKEN (a long-lived access token).")
    parser.add_argument("--dry-run", action="store_true", help="only list the existing resource")
    args = parser.parse_args()
    url, token = os.environ.get("HA_URL"), os.environ.get("HA_TOKEN")
    if not url or not token:
        parser.error("set HA_URL and HA_TOKEN")
    asyncio.run(push(url, token, args.dry_run))


if __name__ == "__main__":
    main()
