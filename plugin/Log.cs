using System;
using System.Collections.Concurrent;

namespace NinaHaTelemetry {

    public static class Log {
        public static Action<string> Info = _ => { };
        public static Action<string> Warn = _ => { };

        private static readonly ConcurrentDictionary<string, DateTime> lastWarned = new();

        public static void WarnThrottled(string key, string message, TimeSpan every) {
            var now = DateTime.UtcNow;
            if (lastWarned.TryGetValue(key, out var last) && now - last < every) return;
            lastWarned[key] = now;
            Warn(message);
        }

        public static void Reset(string key) => lastWarned.TryRemove(key, out _);
    }
}
