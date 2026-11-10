# Plugin loading

How NINA 3.2 finds and loads [[HA Telemetry plugin]].

- Install path: `%LOCALAPPDATA%\NINA\Plugins\3.0.0\HA Telemetry\` (the `3.0.0` folder is NINA's plugin API generation, not the NINA version).
- Plugins load **only at NINA startup**, and a loaded DLL is locked. Never restart NINA on someone's imaging PC without asking; it interrupts imaging.
- **Updating without stopping NINA:** Windows allows renaming or moving an in-use DLL, but not overwriting it. Move the loaded `NINA.HaTelemetry.dll` *out* of the plugins tree (the `PluginLoader` scans `*.dll` recursively), copy the new one in, and it loads at the next restart.
- **Dependency isolation:** each plugin DLL gets its own `PluginAssemblyLoadContext`, which resolves missing assemblies from that plugin's folder. So this plugin's MQTTnet 4.3.7 coexists with other plugins' MQTTnet versions. Assemblies NINA itself ships (Newtonsoft.Json, CommunityToolkit.Mvvm, ProtectedData) come from the default context and must not be bundled ([[Build and test]]).
- **Version pinning:** the current `nina.plugin.template` and the NINA 3.3 nightlies target .NET 10. A 3.3 build won't load in 3.2, and the reverse isn't guaranteed. Pin `NINA.Plugin` to the NINA that is installed, and set `MinimumApplicationVersion` in `AssemblyInfo.cs` to match.
