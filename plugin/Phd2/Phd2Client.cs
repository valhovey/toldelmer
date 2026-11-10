using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NinaHaTelemetry.Phd2 {

    public sealed class Phd2Client : IAsyncDisposable {
        public const string Inactive = "Inactive";
        public const string Disconnected = "Disconnected";

        private const long MaxFitsBytes = 64L * 1024 * 1024;
        private const string WarnKey = "phd2";
        private static readonly string[] CaptureStates = { "Looping", "Guiding", "Calibrating", "Selected", "LostLock" };
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan ActivePoll = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan WarnEvery = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan DisposeLimit = TimeSpan.FromSeconds(3);

        private readonly string host;
        private readonly int port;
        private readonly TimeSpan interval;
        private readonly int maxWidth;
        private readonly Func<bool> isActive;
        private readonly Func<bool> canPublish;
        private readonly Action<GuideFrame.Rendered> publish;
        private readonly CancellationTokenSource cts = new();
        private readonly ConcurrentDictionary<int, TaskCompletionSource<JToken>> pending = new();
        private readonly SemaphoreSlim writeLock = new(1, 1);
        private Task run;
        private int nextId;
        private TcpClient tcp;
        private StreamWriter writer;
        private double lastDx = double.NaN, lastDy = double.NaN;

        public string AppState { get; private set; } = Inactive;
        public bool IsConnected => tcp?.Connected == true;

        public Phd2Client(string host, int port, TimeSpan interval, int maxWidth, Func<bool> isActive, Func<bool> canPublish,
                          Action<GuideFrame.Rendered> publish) {
            this.host = string.IsNullOrWhiteSpace(host) ? "localhost" : host.Trim();
            this.port = port > 0 ? port : 4400;
            this.interval = interval;
            this.maxWidth = maxWidth;
            this.isActive = isActive;
            this.canPublish = canPublish;
            this.publish = publish;
        }

        public void Start() => run = Task.Run(() => Run(cts.Token));

        private async Task Run(CancellationToken ct) {
            var backoff = MinBackoff;
            while (!ct.IsCancellationRequested) {
                if (!isActive()) {
                    AppState = Inactive;
                    backoff = MinBackoff;
                    if (!await Delay(ActivePoll, ct)) break;
                    continue;
                }
                try {
                    await Session(ct);
                    backoff = MinBackoff;
                } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
                    break;
                } catch (Exception ex) {
                    Log.WarnThrottled(WarnKey, $"HA Telemetry: PHD2 {host}:{port} unavailable ({ex.Message}); guide frame paused, " +
                                               "retrying with backoff up to 60 s, logged at most every 10 min", WarnEvery);
                } finally {
                    AppState = Disconnected;
                    CancelPending();
                    writer = null;
                }
                if (!await Delay(backoff, ct)) break;
                backoff = TimeSpan.FromTicks(Math.Min(MaxBackoff.Ticks, backoff.Ticks * 2));
            }
        }

        private async Task Session(CancellationToken ct) {
            using (tcp = new TcpClient()) {
                using (var attempt = CancellationTokenSource.CreateLinkedTokenSource(ct)) {
                    attempt.CancelAfter(ConnectTimeout);
                    await tcp.ConnectAsync(host, port, attempt.Token);
                }
                var stream = tcp.GetStream();
                writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\r\n" };
                var reader = new StreamReader(stream, Encoding.UTF8);
                AppState = "Connected";
                Log.Reset(WarnKey);
                Log.Info($"HA Telemetry: connected to PHD2 at {host}:{port}");

                using var session = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var tasks = new[] { ReadLoop(reader, session.Token), CaptureLoop(session.Token), WhileActive(session.Token) };
                await Task.WhenAny(tasks);
                session.Cancel();
                try { await Task.WhenAll(tasks); } catch { }
            }
        }

        private async Task WhileActive(CancellationToken ct) {
            while (isActive()) await Task.Delay(ActivePoll, ct);
        }

        private async Task ReadLoop(StreamReader reader, CancellationToken ct) {
            while (!ct.IsCancellationRequested) {
                string line = await reader.ReadLineAsync(ct);
                if (line == null) return;
                JObject message;
                try { message = JObject.Parse(line); } catch (JsonException) { continue; }
                if (!TryCompleteCall(message)) OnEvent(message);
            }
        }

        private bool TryCompleteCall(JObject message) {
            if (message["id"] is not JToken id || id.Type != JTokenType.Integer || !pending.TryRemove((int)id, out var call)) return false;
            if (message["error"] is JToken error && error.Type != JTokenType.Null)
                call.TrySetException(new IOException((string)error["message"] ?? "PHD2 error"));
            else
                call.TrySetResult(message["result"]);
            return true;
        }

        private void OnEvent(JObject message) {
            switch ((string)message["Event"]) {
                case "AppState":
                    AppState = (string)message["State"] ?? AppState;
                    break;
                case "GuideStep":
                    AppState = "Guiding";
                    lastDx = (double?)message["dx"] ?? double.NaN;
                    lastDy = (double?)message["dy"] ?? double.NaN;
                    break;
                case "StartGuiding":
                    AppState = "Guiding";
                    break;
                case "StartCalibration":
                    AppState = "Calibrating";
                    break;
                case "LoopingExposures":
                    if (AppState != "Guiding") AppState = "Looping";
                    break;
                case "LoopingExposuresStopped":
                case "GuidingStopped":
                    AppState = "Stopped";
                    lastDx = lastDy = double.NaN;
                    break;
                case "StarLost":
                    AppState = "LostLock";
                    break;
                case "Paused":
                    AppState = "Paused";
                    break;
            }
        }

        private async Task<JToken> Call(string method, CancellationToken ct) {
            var w = writer ?? throw new IOException("not connected");
            int id = Interlocked.Increment(ref nextId);
            var call = new TaskCompletionSource<JToken>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending[id] = call;
            var request = new JObject { ["method"] = method, ["id"] = id }.ToString(Formatting.None);
            await writeLock.WaitAsync(ct);
            try { await w.WriteLineAsync(request); } finally { writeLock.Release(); }
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(CallTimeout);
            using (timeout.Token.Register(() => { if (pending.TryRemove(id, out var c)) c.TrySetCanceled(); }))
                return await call.Task;
        }

        private void CancelPending() {
            foreach (var id in pending.Keys)
                if (pending.TryRemove(id, out var call)) call.TrySetCanceled();
        }

        private async Task CaptureLoop(CancellationToken ct) {
            try { AppState = (string)await Call("get_app_state", ct) ?? AppState; } catch (Exception) when (!ct.IsCancellationRequested) { }
            while (!ct.IsCancellationRequested) {
                await Task.Delay(interval, ct);
                if (!canPublish() || Array.IndexOf(CaptureStates, AppState) < 0) continue;
                try {
                    await CaptureOnce(ct);
                } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
                    throw;
                } catch (Exception ex) {
                    Log.WarnThrottled("phd2-frame", $"HA Telemetry: guide frame capture failed: {ex.Message}", WarnEvery);
                }
            }
        }

        private async Task CaptureOnce(CancellationToken ct) {
            var lockPosition = await Call("get_lock_position", ct) is JArray a && a.Count == 2 ? ((double)a[0], (double)a[1]) : ((double, double)?)null;
            string path = (string)(await Call("save_image", ct))?["filename"];
            if (string.IsNullOrEmpty(path)) return;
            var image = GuideFrame.ParseFits(await ReadAndDelete(path, ct));
            var star = lockPosition is { } lp && !double.IsNaN(lastDx) ? (lp.Item1 + lastDx, lp.Item2 + lastDy) : ((double, double)?)null;
            publish(GuideFrame.Render(image, maxWidth, lockPosition, star));
        }

        private static async Task<byte[]> ReadAndDelete(string path, CancellationToken ct) {
            try {
                var file = new FileInfo(path);
                if (!file.Exists || file.Length > MaxFitsBytes) throw new IOException($"unexpected guide image file ({path})");
                return await File.ReadAllBytesAsync(path, ct);
            } finally {
                try { File.Delete(path); } catch { }
            }
        }

        private static async Task<bool> Delay(TimeSpan delay, CancellationToken ct) {
            try {
                await Task.Delay(delay, ct);
                return true;
            } catch (OperationCanceledException) {
                return false;
            }
        }

        public async ValueTask DisposeAsync() {
            cts.Cancel();
            try { tcp?.Close(); } catch { }
            if (run != null) await Task.WhenAny(run, Task.Delay(DisposeLimit));
        }
    }
}
