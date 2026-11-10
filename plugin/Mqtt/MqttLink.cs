using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace NinaHaTelemetry.Mqtt {

    public sealed record MqttSettings(string Host, int Port, bool UseTls, string Username, string Password, string NodeId);

    public sealed class MqttLink : IAsyncDisposable {
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan IdleWake = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan WarnEvery = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan RediscoveryDelay = TimeSpan.FromSeconds(5);
        private const string WarnKey = "mqtt";

        private readonly MqttSettings settings;
        private readonly Discovery discovery;
        private readonly IMqttClient client;
        private readonly object gate = new();
        private readonly Dictionary<string, byte[]> latest = new();
        private readonly HashSet<string> dirty = new();
        private readonly SemaphoreSlim wake = new(0);
        private readonly CancellationTokenSource cts = new();
        private Task pump;

        public bool IsConnected => client.IsConnected;
        public string LastError { get; private set; }

        public MqttLink(MqttSettings settings, Discovery discovery) {
            this.settings = settings;
            this.discovery = discovery;
            client = new MqttFactory().CreateMqttClient();
            client.ApplicationMessageReceivedAsync += OnMessage;
        }

        public void Start() => pump = Task.Run(() => Run(cts.Token));

        public void Publish(string topic, string payload) => Publish(topic, Encoding.UTF8.GetBytes(payload));

        public void Publish(string topic, byte[] payload) {
            lock (gate) {
                latest[topic] = payload;
                dirty.Add(topic);
            }
            wake.Release();
        }

        public async Task RemoveDiscovery() {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
            timeout.CancelAfter(ConnectTimeout);
            foreach (var topic in discovery.AllConfigTopics()) await Send(topic, Array.Empty<byte>(), timeout.Token);
        }

        private async Task Run(CancellationToken ct) {
            var backoff = MinBackoff;
            while (!ct.IsCancellationRequested) {
                try {
                    if (!client.IsConnected) {
                        await Connect(ct);
                        backoff = MinBackoff;
                    }
                    await Drain(ct);
                    await wake.WaitAsync(IdleWake, ct);
                } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
                    break;
                } catch (Exception ex) {
                    LastError = ex.Message;
                    Log.WarnThrottled(WarnKey, $"HA Telemetry: MQTT {settings.Host}:{settings.Port} unavailable ({ex.Message}); " +
                                               "retrying with backoff up to 60 s, logged at most every 10 min", WarnEvery);
                    try { await Task.Delay(backoff, ct); } catch (OperationCanceledException) { break; }
                    backoff = TimeSpan.FromTicks(Math.Min(MaxBackoff.Ticks, backoff.Ticks * 2));
                }
            }
        }

        private async Task Connect(CancellationToken ct) {
            using (var attempt = CancellationTokenSource.CreateLinkedTokenSource(ct)) {
                attempt.CancelAfter(ConnectTimeout);
                await client.ConnectAsync(BuildOptions(), attempt.Token);
            }
            await client.SubscribeAsync(discovery.HomeAssistantStatus, MqttQualityOfServiceLevel.AtMostOnce, ct);
            await Send(discovery.Availability, Encoding.UTF8.GetBytes("online"), ct);
            await SendDiscovery(ct);
            MarkAllDirty();
            LastError = null;
            Log.Reset(WarnKey);
            Log.Info($"HA Telemetry: connected to {settings.Host}:{settings.Port}");
        }

        private MqttClientOptions BuildOptions() {
            var builder = new MqttClientOptionsBuilder()
                .WithTcpServer(settings.Host, settings.Port)
                .WithClientId($"nina-ha-telemetry-{settings.NodeId}")
                .WithCleanSession()
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(30))
                .WithTimeout(ConnectTimeout)
                .WithWillTopic(discovery.Availability)
                .WithWillPayload("offline")
                .WithWillRetain(true)
                .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce);
            if (!string.IsNullOrEmpty(settings.Username)) builder = builder.WithCredentials(settings.Username, settings.Password ?? "");
            if (settings.UseTls) builder = builder.WithTlsOptions(o => o.UseTls());
            return builder.Build();
        }

        private async Task Drain(CancellationToken ct) {
            while (client.IsConnected) {
                string topic;
                byte[] payload;
                lock (gate) {
                    if (dirty.Count == 0) return;
                    topic = dirty.First();
                    dirty.Remove(topic);
                    payload = latest[topic];
                }
                try {
                    await Send(topic, payload, ct);
                } catch {
                    lock (gate) dirty.Add(topic);
                    throw;
                }
            }
        }

        private async Task SendDiscovery(CancellationToken ct) {
            foreach (var (topic, payload) in discovery.Configs()) await Send(topic, Encoding.UTF8.GetBytes(payload), ct);
        }

        private void MarkAllDirty() {
            lock (gate) dirty.UnionWith(latest.Keys);
        }

        private Task Send(string topic, byte[] payload, CancellationToken ct,
                          MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce) =>
            client.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithRetainFlag(true)
                .WithQualityOfServiceLevel(qos)
                .Build(), ct);

        private Task OnMessage(MqttApplicationMessageReceivedEventArgs e) {
            bool homeAssistantCameOnline = e.ApplicationMessage.Topic == discovery.HomeAssistantStatus &&
                                           Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment) == "online";
            if (homeAssistantCameOnline) _ = Task.Run(Rediscover);
            return Task.CompletedTask;
        }

        private async Task Rediscover() {
            try {
                await Task.Delay(RediscoveryDelay, cts.Token);
                await SendDiscovery(cts.Token);
                MarkAllDirty();
                wake.Release();
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                Log.Warn($"HA Telemetry: rediscovery after Home Assistant restart failed: {ex.Message}");
            }
        }

        public async ValueTask DisposeAsync() {
            cts.Cancel();
            if (pump != null) {
                try { await pump; } catch { }
            }
            try {
                if (client.IsConnected) {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    await Send(discovery.Availability, Encoding.UTF8.GetBytes("offline"), timeout.Token, MqttQualityOfServiceLevel.AtLeastOnce);
                    await client.DisconnectAsync();
                }
            } catch {
            }
            client.Dispose();
            cts.Dispose();
        }
    }
}
