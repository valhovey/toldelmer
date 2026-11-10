using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NinaHaTelemetry.Mqtt {

    public sealed class Discovery {
        public string Prefix { get; }
        public string Node { get; }
        public bool IncludeGuideFrame { get; }
        public string Base => $"nina/{Node}";
        public string Availability => $"{Base}/status";
        public string HomeAssistantStatus => $"{Prefix}/status";

        public string CameraTopic => $"{Base}/camera";
        public string MountTopic => $"{Base}/mount";
        public string GuiderTopic => $"{Base}/guider";
        public string FocuserTopic => $"{Base}/focuser";
        public string FilterWheelTopic => $"{Base}/filterwheel";
        public string ImageTopic => $"{Base}/image";
        public string PreviewTopic => $"{Base}/preview";
        public string AutofocusTopic => $"{Base}/autofocus";
        public string FlipTopic => $"{Base}/flip";
        public string GuideImageTopic => $"{Base}/guide_image";

        private readonly string displayName;
        private readonly string version;

        public Discovery(string prefix, string node, string displayName, string version, bool includeGuideFrame) {
            Prefix = prefix.TrimEnd('/');
            Node = node;
            IncludeGuideFrame = includeGuideFrame;
            this.displayName = displayName;
            this.version = version;
        }

        public IEnumerable<(string Topic, string Payload)> Configs() {
            foreach (var entity in Entities(includeOptional: false))
                yield return (ConfigTopic(entity), ToJson(entity));
        }

        public IEnumerable<string> AllConfigTopics() {
            foreach (var entity in Entities(includeOptional: true))
                yield return ConfigTopic(entity);
        }

        private string ConfigTopic(Entity entity) => $"{Prefix}/{entity.Component}/{Node}/{entity.ObjectId}/config";

        private IEnumerable<Entity> Entities(bool includeOptional) {
            yield return Sensor("camera_temperature", "Camera temperature", CameraTopic, "temperature", unit: "°C", deviceClass: "temperature", measurement: true);
            yield return Sensor("camera_setpoint", "Camera setpoint", CameraTopic, "setpoint", unit: "°C", deviceClass: "temperature");
            yield return Sensor("camera_cooler_power", "Cooler power", CameraTopic, "cooler_power", unit: "%", deviceClass: "power_factor", measurement: true, icon: "mdi:snowflake");
            yield return Binary("camera_cooler_on", "Cooler", CameraTopic, "cooler_on", deviceClass: "running");
            yield return Sensor("camera_state", "Camera state", CameraTopic, "state", icon: "mdi:camera-iris");
            yield return Sensor("camera_gain", "Gain", CameraTopic, "gain", icon: "mdi:tune", diagnostic: true);
            yield return Sensor("camera_offset", "Offset", CameraTopic, "offset", icon: "mdi:tune", diagnostic: true);
            yield return Sensor("exposure_end", "Exposure ends", CameraTopic, "exposure_end", deviceClass: "timestamp");
            yield return Binary("camera_connected", "Camera connected", CameraTopic, "connected", deviceClass: "connectivity", diagnostic: true);

            yield return Sensor("mount_ra", "Right ascension", MountTopic, "ra_hours", unit: "h", icon: "mdi:axis-z-rotate-clockwise");
            yield return Sensor("mount_dec", "Declination", MountTopic, "dec_deg", unit: "°", icon: "mdi:axis-x-rotate-clockwise");
            yield return Sensor("mount_altitude", "Altitude", MountTopic, "alt_deg", unit: "°", measurement: true, icon: "mdi:angle-acute");
            yield return Sensor("mount_azimuth", "Azimuth", MountTopic, "az_deg", unit: "°", measurement: true, icon: "mdi:compass-outline");
            yield return Sensor("mount_pier_side", "Pier side", MountTopic, "pier_side", icon: "mdi:swap-horizontal");
            yield return Sensor("time_to_meridian_flip", "Time to meridian flip", MountTopic, "time_to_flip_min", unit: "min", deviceClass: "duration", icon: "mdi:timer-sand");
            yield return Sensor("mount_pointing", "Pointing", MountTopic, "summary", icon: "mdi:telescope", withAttributes: true);
            yield return Binary("mount_tracking", "Tracking", MountTopic, "tracking", deviceClass: "running");
            yield return Binary("mount_slewing", "Slewing", MountTopic, "slewing", deviceClass: "moving");
            yield return Binary("mount_parked", "Parked", MountTopic, "at_park", icon: "mdi:parking");
            yield return Binary("mount_connected", "Mount connected", MountTopic, "connected", deviceClass: "connectivity", diagnostic: true);

            yield return Sensor("guide_rms_total", "Guide RMS total", GuiderTopic, "rms_total", unit: "″", measurement: true, icon: "mdi:crosshairs-gps");
            yield return Sensor("guide_rms_ra", "Guide RMS RA", GuiderTopic, "rms_ra", unit: "″", measurement: true, icon: "mdi:crosshairs");
            yield return Sensor("guide_rms_dec", "Guide RMS Dec", GuiderTopic, "rms_dec", unit: "″", measurement: true, icon: "mdi:crosshairs");
            yield return Sensor("guide_ra_error", "Guide RA error", GuiderTopic, "ra_error", unit: "″", measurement: true, icon: "mdi:arrow-left-right");
            yield return Sensor("guide_dec_error", "Guide Dec error", GuiderTopic, "dec_error", unit: "″", measurement: true, icon: "mdi:arrow-up-down");
            yield return Binary("guider_guiding", "Guiding", GuiderTopic, "guiding", deviceClass: "running");
            yield return Binary("guider_connected", "Guider connected", GuiderTopic, "connected", deviceClass: "connectivity", diagnostic: true);
            if (IncludeGuideFrame || includeOptional) yield return Image("guide_frame", "Guide frame", GuideImageTopic);

            yield return Binary("meridian_flip", "Meridian flip", FlipTopic, "active", deviceClass: "running", icon: "mdi:rotate-3d-variant");
            yield return Sensor("flip_phase", "Flip phase", FlipTopic, "phase", icon: "mdi:rotate-3d-variant", withAttributes: true);
            yield return Sensor("flip_at", "Flip at", FlipTopic, "flip_at", deviceClass: "timestamp");

            yield return Sensor("focuser_position", "Focuser position", FocuserTopic, "position", unit: "steps", measurement: true, icon: "mdi:focus-field");
            yield return Sensor("focuser_temperature", "Focuser temperature", FocuserTopic, "temperature", unit: "°C", deviceClass: "temperature", measurement: true);
            yield return Binary("focuser_connected", "Focuser connected", FocuserTopic, "connected", deviceClass: "connectivity", diagnostic: true);

            yield return Sensor("filter", "Filter", FilterWheelTopic, "filter", icon: "mdi:filter");
            yield return Binary("filterwheel_connected", "Filter wheel connected", FilterWheelTopic, "connected", deviceClass: "connectivity", diagnostic: true);

            yield return Sensor("last_hfr", "HFR", ImageTopic, "hfr", unit: "px", measurement: true, icon: "mdi:blur", withAttributes: true);
            yield return Sensor("last_hfr_arcsec", "HFR (arcsec)", ImageTopic, "hfr_arcsec", unit: "″", measurement: true, icon: "mdi:blur");
            yield return Sensor("image_scale", "Image scale", ImageTopic, "image_scale", unit: "″/px", icon: "mdi:ruler", diagnostic: true);
            yield return Sensor("last_stars", "Stars", ImageTopic, "stars", measurement: true, icon: "mdi:star-four-points");
            yield return Sensor("last_image_filter", "Last image filter", ImageTopic, "filter", icon: "mdi:filter-outline");
            yield return Sensor("last_image_time", "Last image", ImageTopic, "saved_at", deviceClass: "timestamp");
            yield return Sensor("target", "Target", ImageTopic, "target", icon: "mdi:star-shooting");
            yield return Image("last_preview", "Last subexposure", PreviewTopic);

            yield return Sensor("autofocus", "Last autofocus", AutofocusTopic, "position", unit: "steps", icon: "mdi:image-filter-center-focus", withAttributes: true);
            yield return Sensor("autofocus_hfr", "Autofocus HFR", AutofocusTopic, "hfr", unit: "px", icon: "mdi:blur-radial");
        }

        private static Entity Sensor(string id, string name, string topic, string field, string unit = null, string deviceClass = null,
                                     bool measurement = false, string icon = null, bool diagnostic = false, bool withAttributes = false) {
            var entity = new Entity("sensor", id, name, topic, $"{{{{ value_json.{field} }}}}");
            entity.Set("unit_of_measurement", unit);
            entity.Set("device_class", deviceClass);
            entity.Set("state_class", measurement ? "measurement" : null);
            entity.Set("icon", icon);
            entity.Set("entity_category", diagnostic ? "diagnostic" : null);
            if (withAttributes) {
                entity.Set("json_attributes_topic", topic);
                entity.Set("json_attributes_template", "{{ value_json.attributes | default({}) | tojson }}");
            }
            return entity;
        }

        private static Entity Binary(string id, string name, string topic, string field, string deviceClass = null,
                                     string icon = null, bool diagnostic = false) {
            var entity = new Entity("binary_sensor", id, name, topic, $"{{{{ 'ON' if value_json.{field} else 'OFF' }}}}");
            entity.Set("device_class", deviceClass);
            entity.Set("icon", icon);
            entity.Set("entity_category", diagnostic ? "diagnostic" : null);
            return entity;
        }

        private static Entity Image(string id, string name, string imageTopic) {
            var entity = new Entity("image", id, name, null, null);
            entity.Set("image_topic", imageTopic);
            entity.Set("content_type", "image/jpeg");
            return entity;
        }

        private string ToJson(Entity entity) {
            var json = new JObject {
                ["name"] = entity.Name,
                ["unique_id"] = $"{Node}_{entity.ObjectId}",
                ["default_entity_id"] = $"{entity.Component}.{Node}_{entity.ObjectId}",
                ["availability_topic"] = Availability,
                ["device"] = new JObject {
                    ["identifiers"] = new JArray(Node),
                    ["name"] = displayName,
                    ["manufacturer"] = "N.I.N.A.",
                    ["model"] = "HA Telemetry plugin",
                    ["sw_version"] = version,
                },
            };
            if (entity.StateTopic != null) json["state_topic"] = entity.StateTopic;
            if (entity.ValueTemplate != null) json["value_template"] = entity.ValueTemplate;
            foreach (var (key, value) in entity.Extra) json[key] = value;
            return json.ToString(Formatting.None);
        }

        private sealed record Entity(string Component, string ObjectId, string Name, string StateTopic, string ValueTemplate) {
            public List<(string Key, string Value)> Extra { get; } = new();

            public void Set(string key, string value) {
                if (value != null) Extra.Add((key, value));
            }
        }
    }
}
