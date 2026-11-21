---
created: 2026-11-16T20:48-0800
updated: 2026-11-16T21:10-0800
status: done
---

# Weather and safety monitor telemetry

Publish NINA's Weather (ObservingConditions) and Safety Monitor devices as Home Assistant entities, so the observatory dashboards can show conditions and the safe/unsafe flag. See [[Weather and safety telemetry]].

## Approach
- `TelemetryService` also implements `IWeatherDataConsumer` and `ISafetyMonitorConsumer` and registers with `IWeatherDataMediator` / `ISafetyMonitorMediator` (both injected into `HaTelemetryPlugin`'s MEF constructor, carried in `NinaMediators`). Register/remove consumer only, keeping the [[Read-only contract]].
- Two new state topics: `nina/<node>/weather` and `nina/<node>/safety`.
- Weather: one sensor per `WeatherDataInfo` field, in NINA's (ASCOM) units. Unsupported fields are NaN in NINA and go out as null through `Json.Num`. `average_period`, device name and supported-field count go into attributes.
- Safety: `binary_sensor` with `device_class: safety` (ON = unsafe), unknown when disconnected, plus a connected diagnostic.
- Entities are always discovered, like focuser and filter wheel; they read unknown with no device.
- Dashboard: an optional `dashboard/weather-section.yaml`, like `guide-frame-section.yaml`. README entity table.

## Verification
- `scripts/check.sh` passes; DiscoveryDump diff against the previous output shows only the added configs.
- Read-only audit grep stays clean.
- Not verifiable here: live NINA with a real weather/safety device (Linux box, no NINA).

## Work log
- 2026-11-16 20:48 — decompiled `NINA.Equipment`/`NINA.WPF.Base` 3.2.0.9001; captured baseline DiscoveryDump (46 configs) — starting implementation
- 2026-11-16 21:00 — added consumers, 17 discovery entities, `dashboard/weather-section.yaml`, README table — `scripts/check.sh` OK; DiscoveryDump 46 -> 63 lines, only additions; jinja check of the safety template: null->None, true->OFF, false->ON
- 2026-11-16 21:05 — read-only audit grep clean (only Register/RemoveConsumer on the new mediators); OfflineHarness built but not run (no FITS fixture here, and no harness-covered code changed)
- 2026-11-16 21:10 — not verified: live NINA load with a real weather/safety device, and HA accepting the entities
