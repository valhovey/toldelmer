using NinaHaTelemetry.Mqtt;

string node = args.Length > 0 ? args[0] : "nina_example";
var discovery = new Discovery("homeassistant", node, "NINA (EXAMPLE)", "0.0.0.0", includeGuideFrame: true);
foreach (var (topic, payload) in discovery.Configs()) System.Console.WriteLine($"{topic}\t{payload}");
