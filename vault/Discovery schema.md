# Discovery schema

`plugin/Mqtt/Discovery.cs` is the single source of truth for entities and topics. The [[MQTT link]] publishes each config retained at `<prefix>/<component>/<node>/<object>/config`.

- **Count:** 46 entities with the guide frame, 45 without. The `guide_frame` image entity is included only when the guide-frame interval is above 0 and the profile's guider is PHD2 ([[PHD2 guide frame]]). `AllConfigTopics()` always includes it, so **Remove HA entities** also clears a config left over from an earlier setup.
- **Ids:** `unique_id = <node>_<object>`, plus `default_entity_id = <component>.<node>_<object>`. HA 2025.10 deprecated `object_id` in favour of `default_entity_id`.
- **State:** one JSON topic per device (`nina/<node>/camera`, `mount`, `guider`, `focuser`, `filterwheel`, `image`, `autofocus`, `flip`). Sensors use `{{ value_json.<field> }}`. Binary sensors use `{{ 'ON' if value_json.<field> else 'OFF' }}`.
- **Attributes:** entities built with `withAttributes` read `value_json.attributes` from the same topic (pointing, flip phase, last HFR, autofocus).
- **Images:** `image` entities with `image_topic` and `image/jpeg` (`preview`, `guide_image`).
- **Availability:** every entity uses `nina/<node>/status`.
- **Units:** a sensor with a unit rejects non-numeric states, so every number goes through `Json.Num`/`Json.Finite`, which turn NaN or ∞ into null.

`tools/DiscoveryDump` prints every config without NINA ([[Build and test]]).
