# Toldelmer - NINA in Home Assistant

[![Build](https://github.com/valhovey/toldelmer/actions/workflows/build.yml/badge.svg)](https://github.com/valhovey/toldelmer/actions/workflows/build.yml)

<img width="2770" height="1752" alt="image" src="https://github.com/user-attachments/assets/f6746802-e838-4add-8c59-80b22b47a67e" />

## Overview

Hello! My name is Val, I do a lot of [astrophotography](https://app.astrobin.com/u/valence) in my free time and also am an enthusiast [Home Assistant](https://www.home-assistant.io/) user. For a long time, I've wanted to build a dashboard that enables a read-only view of my observatory in Home Assistant (both for the cool factor, and also because RDP into Windows for NINA is kind of clunky).

As an upfront disclosure, I used an LLM to assist in producing this plugin. I've done my best to both vertically test and build in unit tests for this, but keep in mind that most of this was created using Claude. More on that later, because there are some nice things I have included here if you want to extend or contribute to the project using my fixtures.

As one final LLM disclosure, this Readme is hand-authored but outside of it content will be mixed human-contribution and LLM contribution.

## General Purpose and Philosophy

This plugin is meant to be strictly read-only. It uses the built-in API inside of NINA to get information from your telescope, as well as interactions with PHD2 to get the guide image. All data is broadcast over MQTT to Home Assistant using their standard [Device Discovery Payload schema](https://www.home-assistant.io/integrations/mqtt/#device-discovery-payload). Once installed in NINA and configured with your MQTT broker info the data should be available in Home Assistant.

I also intend for this plugin to be safe for offline use so that, when run at a dark site with no internet or MQTT connection, this plugin effectively does nothing.

## Requirements
* A running [NINA](https://nighttime-imaging.eu/) instance
* [Home Assistant](https://www.home-assistant.io/)
* The [Home Assistant MQTT Integration](https://www.home-assistant.io/integrations/mqtt/)
* A MQTT broker like Home Assistant's [Mosquitto Broker](https://github.com/home-assistant/app-mosquitto)

## Setup

1. Download or build `HA Telemetry/` (see [Build](#build))
2. Close NINA
3. Copy the `HA Telemetry` folder to `%LOCALAPPDATA%\NINA\Plugins\3.0.0\`.
4. Start NINA, navigate to Options -> Plugins -> Toldelmer - HA Telemetry
5. Configure your MQTT broker information
6. Click _Apply and reconnect_, the status should say `Connected to . . .`

Once configured, the entities should show up automatically in Home Assistant under _Settings -> Devices & Services -> MQTT_

## Exposed Entities

| Group | Entities |
|---|---|
| Camera | temperature, setpoint, cooler power and state, camera state, gain, offset, exposure end, connected |
| Mount | RA, Dec, altitude, azimuth, pier side, time to meridian flip, pointing (with LST, hour angle, site lat/lon as attributes), tracking, slewing, parked, connected |
| Guider | RMS total/RA/Dec (″), last RA/Dec error (″), guiding, connected; guide-camera frame (PHD2 only) |
| Meridian flip | flip active, flip phase, flip time |
| Focuser | position, temperature, connected; last autofocus position and HFR, with the focus curve |
| Filter wheel | current filter, connected |
| Last image | HFR (px and "), image scale, stars, filter, target, time, JPEG preview |

There is also an option `Remove HA Entities` that will remove the entities added by this plugin. This is an option in the config for the plugin on NINA.

## Dashboard

I have also included a template dashboard you can use to begin setting up your own observatory dashboard. There are three custom cards:

* `custom:nina-mount-card`: A 3D model of the telescope showing it's current orientation
* `custom:nina-autofocus-card`: A card to display autofocus curves
* `custom:nina-history-card`: a time series chart for guide error, RMS, and HFR

To set this up, add `dashboard/nina-telemetry-cards.js` as a dashboard resource by copying it to `/config/www/` on Home Assistant and add `/local/nina-telemetry-cards.js` as a Javascript module. Alternatively, if you have a HA API token you can push the dashboard using a local python script in this project using `HA_URL=http://homeassistant.local:8123 HA_TOKEN=<long-lived token> uv run --with websockets dashboard/push_resource.py`. Make sure to update your home assistant URL info.

Pick a name for your node ID you configured in the plugin and create a YAML file for your dashboard with `sed 's/nina_NODE/nina_observatory/g' dashboard/nina-dashboard.yaml > my-dashboard.yml`. Then create a new dashboard using the generated YAML.

If you also want to include PHD2, paste in the `dashboard/guide-frame-section.yaml`. You will also need to rename the node ID like you did with the other YAML.

## Building

You need the `.NET 8 SDK` to build this project, which should work on any OS.

1. Install the `.NET 8 SDK`.
2. Turn on the pre-commit hooks to prevent committing code that does not build with `git config core.hooksPath .githooks`.
3. Build and test with `make build` and `make test`

### Python

Optionally, for installing the dashboard into Home Assistant you can set up [uv](https://docs.astral.sh/uv) for the Python environment.

## Working on This Project

This project is set up using my personal LLM environment [Electric Zettel](https://github.com/valhovey/electric-zettel). Documentation made by the LLM and to be consumed by future runs is stored in `vault` as an [Obsidian](https://obsidian.md) vault. It contains two skills:

* `/go` use this whenever interacting with the project. It will both learn from and enrich the existing knowledgebase whenever you ask any question or add any features.
* `/build-skill` to create new skills for specific modalities (e.g. `/ask` for asking questions, not currently created for this repo yet)

## License

[CC BY-NC-SA 4.0](LICENSE)
