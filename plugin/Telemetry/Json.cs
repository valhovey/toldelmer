using System;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace NinaHaTelemetry.Telemetry {

    internal static class Json {
        public static JToken Null => JValue.CreateNull();

        public static JToken Num(double value, int digits = 2) =>
            double.IsFinite(value) ? new JValue(Math.Round(value, digits)) : Null;

        public static JToken Num(JToken token, int digits) =>
            Finite(token) is double value ? new JValue(Math.Round(value, digits)) : Null;

        public static JToken Iso(DateTime time) =>
            time == DateTime.MinValue ? Null : new JValue(time.ToUniversalTime().ToString("o"));

        public static double? Finite(JToken token) {
            if (token == null || token.Type == JTokenType.Null) return null;
            try {
                double value = token.Type == JTokenType.String
                    ? double.Parse((string)token, CultureInfo.InvariantCulture)
                    : (double)token;
                return double.IsFinite(value) ? value : null;
            } catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException or InvalidCastException) {
                return null;
            }
        }
    }
}
