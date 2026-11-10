using System;
using NINA.Equipment.Equipment.MyTelescope;
using Newtonsoft.Json.Linq;
using static NinaHaTelemetry.Telemetry.Json;

namespace NinaHaTelemetry.Telemetry {

    internal sealed class MeridianFlipTracker {
        private const double SiderealRate = 1.0027379;

        private readonly Func<double> minutesAfterMeridian;
        private bool active, sawTrackingStop, success;
        private double targetRa = double.NaN, targetDec = double.NaN;
        private DateTime startedAt = DateTime.MinValue, finishedAt = DateTime.MinValue;
        private string lastPublishedPhase;

        public MeridianFlipTracker(Func<double> minutesAfterMeridian) {
            this.minutesAfterMeridian = minutesAfterMeridian;
        }

        private bool Finished => finishedAt != DateTime.MinValue;

        public void Start(double raHours, double decDegrees) {
            active = true;
            sawTrackingStop = false;
            success = false;
            targetRa = raHours;
            targetDec = decDegrees;
            startedAt = DateTime.UtcNow;
            finishedAt = DateTime.MinValue;
        }

        public void Finish(bool succeeded) {
            active = false;
            success = succeeded;
            finishedAt = DateTime.UtcNow;
        }

        public string TargetDescription => $"target RA {targetRa:0.000} h";

        public JObject Snapshot(TelescopeInfo mount, bool force) {
            bool tracking = mount?.TrackingEnabled == true, slewing = mount?.Slewing == true;
            if (active && !tracking) sawTrackingStop = true;
            string phase = Phase(tracking, slewing);
            if (!force && !active && phase == lastPublishedPhase) return null;
            lastPublishedPhase = phase;

            JToken minutesToMeridian = Null, flipAt = Null;
            if (mount?.Connected == true && !double.IsNaN(targetRa) && (active || !Finished)) {
                double hourAngle = Angles.SignedHourAngle(mount.SiderealTime, targetRa);
                minutesToMeridian = Num(-hourAngle * 60 / SiderealRate, 1);
                if (active) {
                    double hoursUntilFlip = Math.Max(0, (minutesAfterMeridian() / 60.0 - hourAngle) / SiderealRate);
                    flipAt = Iso(DateTime.UtcNow.AddHours(hoursUntilFlip));
                }
            }
            var state = new JObject {
                ["active"] = active,
                ["phase"] = phase,
                ["minutes_to_meridian"] = minutesToMeridian,
                ["flip_at"] = flipAt,
                ["started_at"] = Iso(startedAt),
                ["finished_at"] = Iso(finishedAt),
                ["success"] = Finished ? success : null,
                ["target_ra_hours"] = Num(targetRa, 5),
                ["target_dec_deg"] = Num(targetDec, 4),
            };
            state["attributes"] = state.DeepClone();
            return state;
        }

        private string Phase(bool tracking, bool slewing) {
            if (active) {
                if (!tracking) return "Waiting for meridian";
                if (slewing) return "Flipping";
                return sawTrackingStop ? "Recentering" : "Starting";
            }
            if (Finished) return success ? "Done" : "Failed";
            return "Idle";
        }
    }

    internal static class Angles {
        public static double Wrap24(double hours) => ((hours % 24) + 24) % 24;

        public static double SignedHourAngle(double siderealTime, double rightAscension) {
            double hourAngle = Wrap24(siderealTime - rightAscension);
            return hourAngle > 12 ? hourAngle - 24 : hourAngle;
        }
    }
}
