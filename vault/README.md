# nina-ha-telemetry vault

Working knowledge for **HA Telemetry**, a read-only N.I.N.A. 3.2 plugin that publishes imaging telemetry to MQTT as Home Assistant discovery entities. Start with [[HA Telemetry plugin]].

## The plugin
- [[HA Telemetry plugin]]: architecture, the classes and how they fit together
- [[Read-only contract]]: what the plugin may and may not touch
- [[NINA data sources]]: which mediator, event or file each value comes from (decompiled 3.2.0.9001)
- [[Discovery schema]]: entities, topics, `default_entity_id`
- [[MQTT link]]: the "latest wins" publish pump, LWT, HA birth, shutdown order
- [[Offline behaviour]]: timeouts, backoff, log throttling, what never blocks NINA
- [[Meridian flip tracking]]: phase model and flip time
- [[Autofocus telemetry]]: the live curve plus the report file
- [[Options page]]: settings, DPAPI password, WPF theming gotcha

## PHD2 (optional)
- [[PHD2 guide frame]]: the second JSON-RPC client, its gate, and the capture loop
- [[Guide frame rendering]]: FITS parsing, auto-STF, degenerate frames, row order

## Home Assistant side
- [[Dashboard cards]]: the three custom cards and the dashboard template

## Building, testing, shipping
- [[Build and test]]: SDK, build, DiscoveryDump, OfflineHarness
- [[Plugin loading]]: install path, load contexts, updating without stopping NINA, NINA version pinning

## Working in this repo
- [[Agent skills]]: the `go` and `build-skill` Claude Code skills and the no-comments rule
- Plans: [plans/README.md](plans/README.md)
