# Toldelmer - HA Telemetry for N.I.N.A.

[![Build](https://github.com/valhovey/toldelmer/actions/workflows/build.yml/badge.svg)](https://github.com/valhovey/toldelmer/actions/workflows/build.yml)

A [N.I.N.A.](https://nighttime-imaging.eu/) 3.2 plugin that publishes live imaging telemetry to an MQTT broker as
[Home Assistant MQTT discovery](https://www.home-assistant.io/integrations/mqtt/#mqtt-discovery) entities. It hooks
NINA's equipment mediators directly, so it works without any sequence instructions or triggers.

- **Read-only.** The plugin only observes NINA. It never commands, connects or configures equipment, and it accepts
  no commands over MQTT.
- **PHD2 is optional.** Everything works with any guider, or none at all. If NINA's guider is PHD2, the plugin can also
  publish PHD2's current guide-camera frame.
- **Safe offline.** With no broker in reach (say, at a dark site) it retries quietly in the background and never
  slows NINA down.

## What you get

46 entities (45 without the PHD2 guide frame) on one Home Assistant device, `NINA (<PC name>)`:

| Group | Entities |
|---|---|
| Camera | temperature, setpoint, cooler power and state, camera state, gain, offset, exposure end, connected |
| Mount | RA, Dec, altitude, azimuth, pier side, time to meridian flip, pointing (with LST, hour angle, site lat/lon as attributes), tracking, slewing, parked, connected |
| Guider | RMS total/RA/Dec (″), last RA/Dec error (″), guiding, connected; guide-camera frame (PHD2 only) |
| Meridian flip | flip active, flip phase (Starting → Waiting for meridian → Flipping → Recentering → Done/Failed), flip time |
| Focuser | position, temperature, connected; last autofocus position and HFR, with the whole curve and fit as attributes |
| Filter wheel | current filter, connected |
| Last image | HFR (px and ″), image scale, stars, filter, target, time, JPEG preview |

## Install

1. Download or build `HA Telemetry/` (see [Build](#build)).
2. Close NINA. Plugins load only at startup.
3. Copy the `HA Telemetry` folder to `%LOCALAPPDATA%\NINA\Plugins\3.0.0\`.
4. Start NINA and open **Options → Plugins → Toldelmer - HA Telemetry**. Enter your broker host, port, username and password,
   then click **Apply and reconnect**. The status line should say `Connected to …`.

The entities appear in Home Assistant under **Settings → Devices & services → MQTT**.

### Options

| Option | Default | Notes |
|---|---|---|
| Broker host / port / TLS | — / 1883 / off | Your MQTT broker, e.g. the Mosquitto add-on |
| Username / password | — | The password is encrypted for the current Windows user (DPAPI) in the NINA profile |
| Discovery prefix | `homeassistant` | Must match Home Assistant's MQTT discovery prefix |
| Node id | `nina_<pc name>` | Used in topics and entity ids. Changing it orphans the old entities |
| Preview width | 1280 | Max width of the JPEG preview in px; 0 turns the preview off |
| PHD2 guide frame every | 10 s | 0 turns it off. It only runs while NINA is connected to PHD2 |

**Remove HA entities** clears every discovery config this plugin published, which deletes the device in Home Assistant.

## Dashboard

`dashboard/` has a ready-made Home Assistant dashboard, with three custom cards:

- `custom:nina-mount-card`: a 3D German equatorial mount that follows the telescope (three.js, loaded from esm.sh).
- `custom:nina-autofocus-card`: the last autofocus curve with NINA's fit.
- `custom:nina-history-card`: a time series with real axes, used for guide error, RMS and HFR.

To set it up:

1. Add `dashboard/nina-telemetry-cards.js` as a dashboard resource. Pick one of these:
   - Copy it to `/config/www/` on Home Assistant and add `/local/nina-telemetry-cards.js` as a **JavaScript module**
     under **Settings → Dashboards → ⋮ → Resources**.
   - Or, with no file access, run
     `HA_URL=http://homeassistant.local:8123 HA_TOKEN=<long-lived token> uv run --with websockets dashboard/push_resource.py`.
     This installs it inline as a `data:` URL.
2. Make the dashboard YAML for your node id (the `Node id` option, e.g. `nina_observatory`):
   ```sh
   sed 's/nina_NODE/nina_observatory/g' dashboard/nina-dashboard.yaml > my-dashboard.yaml
   ```
3. In Home Assistant, create a new dashboard, open **⋮ → Edit → ⋮ → Raw configuration editor** and paste
   `my-dashboard.yaml`.
4. Using PHD2? Paste `dashboard/guide-frame-section.yaml` (after the same `sed`) as an extra section to add the guide frame.

The mount card uses the site latitude NINA reports. Set `latitude:` on the card to override it.

## MQTT topics

State lives on one retained JSON topic per device, under `nina/<node>/`:
`status` (`online`/`offline`, LWT), `camera`, `mount`, `guider`, `focuser`, `filterwheel`, `image`, `preview` (JPEG),
`guide_image` (JPEG), `autofocus`, `flip`. Discovery configs are at `<prefix>/<component>/<node>/<object>/config`.
The schema is `plugin/Mqtt/Discovery.cs`.

## Build

### Set up a dev environment

You need **git** and the **.NET 8 SDK**. No NINA install is needed to build: the `NINA.Plugin` package and MQTTnet
come from nuget.org on the first build. Windows, macOS and Linux all work (`EnableWindowsTargeting`), but the plugin
only runs inside NINA on Windows.

1. Install the .NET 8 SDK:
   - Windows: `winget install Microsoft.DotNet.SDK.8`
   - macOS / Linux, user-local, no admin rights:
     ```sh
     curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0
     export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH   # add to your shell profile
     ```
   - Or the installer from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0).

   `dotnet --list-sdks` should list an `8.0.x` SDK. A newer SDK can also build `net8.0` projects.
2. Turn on the pre-commit hook, once per clone:
   ```sh
   git config core.hooksPath .githooks
   ```
   Every commit then runs `scripts/check.sh` and is refused if the build fails. Use `git commit --no-verify` to skip
   it once.
3. Run the check yourself:
   ```sh
   scripts/check.sh      # builds the plugin and tools, smoke-tests DiscoveryDump; prints "check: OK"
   ```

Optional, for the dashboard: [uv](https://docs.astral.sh/uv/) runs `dashboard/push_resource.py`, and Node.js
(`node --check`) syntax-checks the cards.

### Build and test

```sh
cd plugin && dotnet build -c Release      # stages ../dist/HA Telemetry/
```

Checks you can run without NINA:

```sh
cd tools/DiscoveryDump && dotnet run -- nina_example                   # print every discovery config
cd tools/OfflineHarness && dotnet run -- <phd2-frame.fits> <out-dir>   # offline, PHD2 and render checks
```

The harness needs one FITS frame saved by PHD2 (**File → Save Image**) and prints `ALL PASS` when it succeeds. It is
not part of `scripts/check.sh` or CI, because the frame is not in the repo.

### CI

GitHub Actions (`.github/workflows/build.yml`) runs `scripts/check.sh` on Ubuntu for every push and pull request to
`main`. The built `HA Telemetry/` folder is attached to each run as the `HA-Telemetry` artifact.

## Compatibility

Built against `NINA.Plugin 3.2.0.9001` (`net8.0-windows`). NINA 3.3 nightlies move to .NET 10, so a 3.2 build may not
load there.

## Working on it

Project knowledge lives in `vault/` (start at `vault/README.md`). If you use Claude Code, `/go <task>` runs a
plan-and-document loop over that vault, and `/build-skill <name> - <goal>` makes narrower variants of it.

## License

[CC BY-NC-SA 4.0](LICENSE) © 2026 Val Hovey.
