# Weather and safety telemetry

How [[HA Telemetry plugin]] publishes NINA's Weather device (ASCOM ObservingConditions, OpenWeatherMap, Weather Underground, …) and Safety Monitor to Home Assistant.

## NINA side (decompiled 3.2.0.9001)
- `IWeatherDataMediator` / `IWeatherDataConsumer` push a `WeatherDataInfo` every `DevicePollingInterval`, like the other equipment mediators ([[NINA data sources]]).
- `WeatherDataInfo` fields, all `double`, in ASCOM ObservingConditions units: `Temperature` °C, `DewPoint` °C, `Humidity` %, `Pressure` hPa, `CloudCover` %, `RainRate` mm/h, `SkyBrightness` lux, `SkyQuality` mag/arcsec², `SkyTemperature` °C, `StarFWHM` ″, `WindSpeed` m/s, `WindGust` m/s, `WindDirection` °, `AveragePeriod` h.
- A field the driver doesn't support is **NaN** (the ASCOM wrapper's default, and the web drivers hard-code NaN for sky fields and gusts). `Json.Num` turns that into null, so HA shows unknown.
- `ISafetyMonitorMediator` / `ISafetyMonitorConsumer` push `SafetyMonitorInfo.IsSafe`. On disconnect NINA resets the info, so `IsSafe` reads false; the plugin reports null (unknown) instead when not connected. The mediator also has an `IsSafeChanged` event, but the consumer broadcast happens in the same poll, so the plugin doesn't use it.

## MQTT / HA side
- Topics `nina/<node>/weather` and `nina/<node>/safety` ([[Discovery schema]]).
- Weather sensors use HA device classes where one fits (`temperature`, `humidity`, `atmospheric_pressure`, `precipitation_intensity`, `illuminance`, `wind_speed`). Wind direction, sky quality, star FWHM and cloud cover have none and use icons.
- Safety is a `binary_sensor` with `device_class: safety`, where **ON means unsafe**. Its template emits `None` while disconnected so the state is unknown, not "safe".
- Dashboard section: `dashboard/weather-section.yaml` ([[Dashboard cards]]).

## Gotchas
- The entities are always discovered, even with no weather device, the same as focuser and filter wheel. With no device they stay unknown.
- Don't invert the safety template by accident: HA's `safety` class is "on = unsafe", the opposite of NINA's `IsSafe`.
