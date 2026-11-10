# Options page

`plugin/Options.xaml` (Options → Plugins → Toldelmer - HA Telemetry). NINA looks up the DataTemplate key `"<plugin name>_Options"`, here `Toldelmer - HA Telemetry_Options`. It must match `AssemblyTitle` in `Properties/AssemblyInfo.cs` exactly (presumably the page shows no settings otherwise; not tested). Its DataContext is the `HaTelemetryPlugin` instance ([[HA Telemetry plugin]]).

- Settings are per NINA profile, via `PluginOptionsAccessor` keyed by the assembly GUID in `Properties/AssemblyInfo.cs`. **Never change that GUID.** It is the plugin's identity and its options key.
- The password is stored DPAPI-encrypted (CurrentUser) under `PasswordDpapi`, so the profile XML never holds it in clear. A profile copied to another Windows user loses it.
- `NodeId` is slugged to `[a-z0-9_]`, and an empty value falls back to `nina_<machine name>`. Changing it orphans the old HA entities ([[Discovery schema]]).
- A profile change raises PropertyChanged for every property (empty name) and restarts the plugin.

## Gotchas
- **No implicit styles in the DataTemplate.** A local `Style TargetType="TextBox"` *replaces* NINA's themed implicit style instead of extending it. The boxes then render as default white Windows controls, unreadable in a dark theme. Keep the Grid layout-only.
- `PasswordBox.Password` can't be bound in WPF, so `Options.xaml.cs` copies it by hand on `Loaded` and `PasswordChanged`.
