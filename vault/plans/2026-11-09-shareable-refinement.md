---
created: 2026-11-09T14:00-05:00
updated: 2026-11-09T15:30-05:00
status: done
---

# Shareable refinement pass

Make the plugin shareable with other NINA users: a standalone repo, PHD2 not required, strictly read-only, no personal data, no code comments (knowledge moved to this vault), a generic dashboard template, and the `go`/`build-skill` skills.

## Plan
1. Move the project out of the author's personal monorepo into its own git repo. Leave the personal dashboard and the SSH test script behind.
2. PHD2: create the client only when the interval is above 0 and the profile guider is PHD2. Open sockets only while NINA's connected guider is PHD2. Publish the `guide_frame` entity only when PHD2 is in use.
3. Audit every NINA, PHD2 and MQTT interaction for writes ([[Read-only contract]]).
4. Refactor: split `TelemetryService` into flip and autofocus trackers plus JSON and JPEG helpers; named-argument discovery helpers; a settings-accessor helper; serialised start/stop; a record for rendered frames.
5. Remove every comment; capture the reasons as vault pages.
6. Generic dashboard: `nina-*` cards (no AllSky, no hard-coded site), a YAML template, and an env-driven resource pusher.
7. License CC BY-NC-SA 4.0; README for end users.

## Work log
- 2026-11-09 14:10 — rsynced the plugin, tools and README to `~/Documents/Programs/NET/nina-ha-telemetry` and moved the (empty) `.git` there; added `.gitignore`. The dashboard and `enable-ssh.ps1` stay in the author's monorepo.
- 2026-11-09 14:15 — added the CC BY-NC-SA 4.0 legal text as LICENSE from creativecommons.org; AssemblyInfo metadata updated; version 0.3.0.0.
- 2026-11-09 14:40 — rewrote the plugin sources without comments; split TelemetryService; added the PHD2 gate (`GuiderInfo.DeviceId` / `GuiderSettings.GuiderName` start with `PHD2`; NINA's PHD2 driver id is `PHD2_Single`, from decompiling). Build: one error (`AutoFocusInfo` lives in `NINA.Equipment.Interfaces.Mediator`), fixed; then clean.
- 2026-11-09 14:55 — OfflineHarness updated for the new APIs, plus a check that the inactive gate opens no socket: ALL PASS. Refactor check: DiscoveryDump output identical to 0.2.1 (46 configs, ignoring sw_version/name), and `guide.ppm` byte-identical.
- 2026-11-09 15:00 — read-only audit: only RegisterConsumer/RemoveConsumer and event subscriptions on mediators; profile reads; PHD2 `get_app_state`/`get_lock_position`/`save_image`; one MQTT subscription (HA birth). Clean.
- 2026-11-09 15:15 — dashboard: generated `nina-telemetry-cards.js` from the personal cards (AllSky card dropped, renamed, comments stripped, `node --check` ok), `nina-dashboard.yaml` and `guide-frame-section.yaml` (every entity id checked against DiscoveryDump), and a generic `push_resource.py`.
- 2026-11-09 15:30 — vault bootstrapped with 15 concept pages; `go` and `build-skill` copied and generalised (project ground rules replace the monorepo's private-files hook rules).

## Not done / open
- Not yet loaded in a live NINA. The PHD2 gate and the conditional guide-frame entity are tested only through the harness.
