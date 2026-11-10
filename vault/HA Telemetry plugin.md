# HA Telemetry plugin

The NINA plugin in `plugin/` (`NINA.HaTelemetry.dll`, `net8.0-windows`, `NINA.Plugin 3.2.0.9001`, MQTTnet 4.3.7). NINA composes it with MEF at startup. It observes the equipment mediators and publishes retained JSON to MQTT, and Home Assistant picks that up through [[Discovery schema|discovery]]. It is strictly observational; see [[Read-only contract]].

**Naming:** NINA shows it as **Toldelmer - HA Telemetry** (`AssemblyTitle`/`AssemblyProduct`, as of 2026-11-09), and the GitHub repo is `toldelmer`. Everything internal keeps the old name on purpose: `NINA.HaTelemetry.dll`, namespace `NinaHaTelemetry`, install folder `HA Telemetry/`, the `HA Telemetry:` log prefix, MQTT client id `nina-ha-telemetry-<node>`, the HA device model, and the GUID ([[Options page]], [[Plugin loading]]).

## Classes
- `HaTelemetryPlugin`: the MEF manifest and [[Options page]] view model. It owns the lifecycle. `Initialize` starts in the background, `Teardown` stops, and a profile change restarts. Start and stop are serialised on a `SemaphoreSlim`, because Initialize, ProfileChanged and Apply can all fire close together.
- `Mqtt/Discovery`: every entity, topic and template. See [[Discovery schema]].
- `Mqtt/MqttLink`: the connection and the publish pump. See [[MQTT link]].
- `Telemetry/TelemetryService`: the mediator consumer for camera, mount, guider, focuser and filter wheel, plus the image-saved and flip handlers. It builds the JSON for each topic. Every callback goes through `Guard`, which catches, throttle-logs and never throws back into NINA.
- `Telemetry/MeridianFlipTracker`: the flip state machine. See [[Meridian flip tracking]].
- `Telemetry/AutofocusTracker`: the AF points, the report watcher and the summary. See [[Autofocus telemetry]].
- `Telemetry/Json`: `Num` (rounds, turns non-finite into null), `Iso`, and `Finite` (parses the string "NaN" too).
- `Telemetry/JpegEncoder`: WPF JPEG encoding at quality 80 for the preview and the guide frame.
- `Phd2/Phd2Client` and `Phd2/GuideFrame`: optional, see [[PHD2 guide frame]] and [[Guide frame rendering]].
- `Log`: a logging seam. The plugin points it at NINA's `Logger`, and the [[Build and test|offline harness]] points it at the console. `MqttLink`, `Phd2Client` and `GuideFrame` take no NINA or WPF types, so they compile into the harness on macOS.

## Design choices
- **C#, not a Python sidecar.** NINA plugins are .NET assemblies. A sidecar would need the third-party Advanced API plugin, a second process and polling. Hand-written discovery JSON is small.
- **One JSON topic per device**, and each entity picks its field with `value_template`. That means a few publishes per update instead of one per entity.
- **No comments in the code.** The reasons live in this vault ([[Agent skills]]).

## Related
[[NINA data sources]] · [[Offline behaviour]] · [[Plugin loading]] · [[Dashboard cards]]
