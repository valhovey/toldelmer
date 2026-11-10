using System.Reflection;
using System.Runtime.InteropServices;

[assembly: Guid("6b0f3e2a-7c41-4d8e-9a55-2f1d0c8e4b73")]
[assembly: AssemblyTitle("Toldelmer - HA Telemetry")]
[assembly: AssemblyDescription("Publishes live, read-only NINA telemetry to MQTT using Home Assistant discovery. No sequence instructions needed.")]
[assembly: AssemblyCompany("Val Hovey")]
[assembly: AssemblyProduct("Toldelmer - HA Telemetry")]
[assembly: AssemblyCopyright("Copyright © 2026 Val Hovey")]
[assembly: AssemblyVersion("0.3.0.0")]
[assembly: AssemblyFileVersion("0.3.0.0")]
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.2.0.9001")]
[assembly: AssemblyMetadata("License", "CC BY-NC-SA 4.0")]
[assembly: AssemblyMetadata("LicenseURL", "https://creativecommons.org/licenses/by-nc-sa/4.0/")]
[assembly: AssemblyMetadata("Repository", "")]
[assembly: AssemblyMetadata("Homepage", "")]
[assembly: AssemblyMetadata("Tags", "MQTT,Home Assistant,Telemetry")]
[assembly: AssemblyMetadata("LongDescription", "Connects to an MQTT broker and publishes camera, mount, guider, focuser, filter wheel, image (HFR, stars, JPEG preview), meridian flip and autofocus data as Home Assistant MQTT discovery entities. It only observes NINA: it never commands equipment and accepts no commands over MQTT. If NINA's guider is PHD2, it can also publish PHD2's current guide frame. PHD2 is optional.")]
[assembly: ComVisible(false)]
