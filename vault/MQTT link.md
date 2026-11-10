# MQTT link

`plugin/Mqtt/MqttLink.cs` is one long-lived MQTTnet client per plugin start. See [[HA Telemetry plugin]].

- **Publish never blocks.** `Publish(topic, payload)` stores the payload in a "latest wins" map and marks the topic dirty. One pump task drains it, so a slow broker can't build a backlog. Every state topic is retained.
- **Replay:** on (re)connect the link publishes `online`, re-sends discovery and marks every cached topic dirty. When Home Assistant restarts it publishes `online` on `<prefix>/status`, and the link waits 5 s, then re-sends discovery and state.
- **LWT:** retained `offline` on `nina/<node>/status` at QoS 1.
- Connect, backoff and log throttling are on [[Offline behaviour]].

## Gotchas
- **Shutdown order:** `DisposeAsync` cancels the pump *before* the clean DISCONNECT. Otherwise the pump sees the disconnect, reconnects and republishes retained `online`, and HA shows NINA as up after it has closed. A clean DISCONNECT also makes the broker discard the will, so `offline` is published by hand at QoS 1 just before disconnecting.
- MQTTnet's `WithTimeout` does not bound the TCP connect itself, so each attempt also gets a 10 s cancellation (see [[Offline behaviour]]).
