using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NINA.Equipment.Interfaces.Mediator;
using Newtonsoft.Json.Linq;
using OxyPlot;
using static NinaHaTelemetry.Telemetry.Json;

namespace NinaHaTelemetry.Telemetry {

    internal sealed class AutofocusTracker : IDisposable {
        public static readonly string ReportDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NINA", "AutoFocus");

        private static readonly TimeSpan ReportSettleDelay = TimeSpan.FromMilliseconds(1500);

        private readonly Action<JObject> publish;
        private readonly object gate = new();
        private readonly List<double[]> points = new();
        private readonly FileSystemWatcher watcher;
        private JObject summary = new() { ["status"] = "none" };

        public AutofocusTracker(Action<JObject> publish) {
            this.publish = publish;
            if (!Directory.Exists(ReportDirectory)) return;
            watcher = new FileSystemWatcher(ReportDirectory, "*.json") { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite };
            watcher.Created += (_, e) => _ = LoadReport(e.FullPath, ReportSettleDelay);
            watcher.EnableRaisingEvents = true;
            var newest = new DirectoryInfo(ReportDirectory).GetFiles("*.json").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
            if (newest != null) _ = LoadReport(newest.FullName, TimeSpan.Zero);
        }

        public void RunStarting() {
            lock (gate) {
                points.Clear();
                summary = new JObject { ["status"] = "running", ["started_at"] = DateTime.UtcNow.ToString("o") };
            }
            Publish();
        }

        public void AddPoint(DataPoint point) {
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) return;
            lock (gate) points.Add(new[] { Math.Round(point.X, 1), Math.Round(point.Y, 3), 0d });
            Publish();
        }

        public void RunEnded(AutoFocusInfo info) {
            lock (gate) {
                summary["status"] = "done";
                summary["position"] = Math.Round(info.Position);
                summary["filter"] = info.Filter;
                summary["temperature"] = Num(info.Temperature, 1);
                summary["finished_at"] = info.Timestamp.ToUniversalTime().ToString("o");
            }
            Publish();
        }

        private async Task LoadReport(string path, TimeSpan delay) {
            try {
                await Task.Delay(delay);
                var report = JObject.Parse(await File.ReadAllTextAsync(path));
                var measured = (report["MeasurePoints"] as JArray ?? new JArray())
                    .Select(p => (Position: Finite(p["Position"]), Hfr: Finite(p["Value"]), Error: Finite(p["Error"])))
                    .Where(p => p.Position.HasValue && p.Hfr.HasValue)
                    .Select(p => new[] { Math.Round(p.Position.Value, 1), Math.Round(p.Hfr.Value, 3), Math.Round(p.Error ?? 0, 3) })
                    .ToList();
                lock (gate) {
                    points.Clear();
                    points.AddRange(measured);
                    summary = new JObject {
                        ["status"] = "done",
                        ["position"] = Num(report.SelectToken("CalculatedFocusPoint.Position"), 0),
                        ["hfr"] = Num(report.SelectToken("CalculatedFocusPoint.Value"), 3),
                        ["previous_position"] = Num(report.SelectToken("PreviousFocusPoint.Position"), 0),
                        ["previous_hfr"] = Num(report.SelectToken("PreviousFocusPoint.Value"), 3),
                        ["initial_position"] = Num(report.SelectToken("InitialFocusPoint.Position"), 0),
                        ["filter"] = report["Filter"],
                        ["temperature"] = Num(report["Temperature"], 2),
                        ["method"] = report["Method"],
                        ["fitting"] = report["Fitting"],
                        ["finished_at"] = report["Timestamp"],
                        ["duration"] = report["Duration"],
                        ["r_squared"] = report["RSquares"],
                        ["fittings"] = report["Fittings"],
                        ["intersections"] = report["Intersections"],
                        ["report"] = Path.GetFileName(path),
                    };
                }
                Publish();
            } catch (Exception ex) {
                Log.Warn($"HA Telemetry: autofocus report {Path.GetFileName(path)}: {ex.Message}");
            }
        }

        private void Publish() {
            JObject attributes;
            lock (gate) {
                attributes = (JObject)summary.DeepClone();
                attributes["points"] = JArray.FromObject(points);
            }
            publish(new JObject {
                ["position"] = attributes["position"] ?? Null,
                ["hfr"] = attributes["hfr"] ?? Null,
                ["attributes"] = attributes,
            });
        }

        public void Dispose() => watcher?.Dispose();
    }
}
