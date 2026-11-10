# Autofocus telemetry

`Telemetry/AutofocusTracker.cs` feeds `nina/<node>/autofocus` (state = chosen position; attributes = points, fit, R²). The [[Dashboard cards|autofocus card]] draws it.

- **Live:** `AutoFocusRunStarting` clears the points, each `NewAutoFocusPoint` (X = position, Y = HFR) appends `[pos, hfr, 0]`, and `UpdateEndAutoFocusRun` marks it done ([[NINA data sources]]).
- **Report:** NINA then writes `%LOCALAPPDATA%\NINA\AutoFocus\yyyy-MM-dd--HH-mm-ss--<profileId>.json` (`MeasurePoints[{Position,Value,Error}]`, `CalculatedFocusPoint`, `Fittings`, `RSquares`, `Intersections`). A `FileSystemWatcher` reads it 1.5 s after creation and replaces the live points with the measured ones and their error bars.
- **Startup:** the newest report is loaded right away, so a dashboard has a curve before the night's first run.

## Gotchas
- **The report writes NaN as the string `"NaN"`** (e.g. `InitialFocusPoint.Value`). Every number goes through `Json.Finite`, which parses strings and drops non-finite values, because a sensor with a unit rejects a non-numeric state.
