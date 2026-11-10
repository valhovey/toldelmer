# Read-only contract

The promise [[HA Telemetry plugin]] makes: it **observes** NINA and never changes the imaging session.

## What it does touch
- **NINA:** `RegisterConsumer`/`RemoveConsumer` on the camera, telescope, filter wheel, focuser and guider mediators, plus event subscriptions (`GuideEvent`, `GuidingStarted/Stopped`, `ImageSaved`, `Before/AfterMeridianFlip`). Reads of `ActiveProfile.GuiderSettings` (`GuiderName`, `PHD2ServerUrl`, `PHD2ServerPort`) and `MeridianFlipSettings.MinutesAfterMeridian`. The flip handlers return `Task.CompletedTask` at once, because NINA awaits them inside its flip workflow.
- **NINA profile:** only the plugin's own options, through `PluginOptionsAccessor` keyed by the assembly GUID (see [[Options page]]).
- **Files:** it reads `%LOCALAPPDATA%\NINA\AutoFocus\*.json` ([[Autofocus telemetry]]). It reads and then deletes the temp FITS that PHD2's `save_image` writes, because PHD2's API says the client deletes it ([[PHD2 guide frame]]).
- **PHD2:** only `get_app_state`, `get_lock_position` and `save_image`. None of them changes guiding state.
- **MQTT:** publishes. Its only subscription is `<prefix>/status`, Home Assistant's birth message, which triggers a re-send of discovery ([[MQTT link]]). No command topics.
- **Home Assistant:** the **Remove HA entities** button publishes empty retained configs, which deletes only this plugin's device.

## Keeping it true
- Never call a mediator method that acts (connect, slew, move, set temperature, start guiding). The audit is a grep for `nina.<Mediator>.<Member>` and `Call("…")` in `plugin/`. The list above should be the whole result.
- Don't add MQTT command topics or `command_topic` entities.

Last audited 2026-11-09 (0.3.0.0).
