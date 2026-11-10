# Offline behaviour

At a dark-sky site there is often no LAN, PHD2 may not be running, and NINA must not notice. The rules in [[HA Telemetry plugin]]:

- No network work on NINA's startup path, in a mediator callback or in a flip handler. `Initialize` starts the link on `Task.Run`, and callbacks only fill the [[MQTT link]] map.
- **MQTT:** each connect attempt is capped at 10 s, then backoff 2 → 60 s. Without the cap, an unroutable broker waits for the OS connect timeout (about 21 s on Windows, 75 s on macOS).
- **PHD2:** connect 5 s, RPC 10 s, backoff 5 → 60 s. No connection at all unless NINA's guider is PHD2 ([[PHD2 guide frame]]).
- **Logging:** one warning per outage, then at most every 10 min (`Log.WarnThrottled`). `Log.Reset` on reconnect, so the next outage is reported immediately.
- **No wasted work:** guide-frame capture is skipped while MQTT is down, so PHD2 writes no FITS for nothing.
- **Bounded teardown:** the PHD2 client gives up waiting after 3 s. Its `CancellationTokenSource` is deliberately not disposed, so a loop still unwinding after that can't hit `ObjectDisposedException`.

Verified by `tools/OfflineHarness`: black-hole, refused and DNS-failure brokers; PHD2 not running or unreachable; a fake PHD2 that dies mid-session; and the inactive gate ([[Build and test]]).
