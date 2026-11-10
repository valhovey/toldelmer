using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NINA.Core.Enum;
using NINA.Core.Interfaces;
using NINA.Equipment.Equipment.MyCamera;
using NINA.Equipment.Equipment.MyFilterWheel;
using NINA.Equipment.Equipment.MyFocuser;
using NINA.Equipment.Equipment.MyGuider;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Equipment.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.Mediator;
using NinaHaTelemetry.Mqtt;
using NinaHaTelemetry.Phd2;
using OxyPlot;
using static NinaHaTelemetry.Telemetry.Json;

namespace NinaHaTelemetry.Telemetry {

    public sealed record NinaMediators(ICameraMediator Camera, ITelescopeMediator Telescope, IFilterWheelMediator FilterWheel,
                                       IFocuserMediator Focuser, IGuiderMediator Guider, IImageSaveMediator ImageSave);

    public sealed class TelemetryService :
        ICameraConsumer, ITelescopeConsumer, IFilterWheelConsumer, IFocuserConsumer, IGuiderConsumer, IDisposable {

        private const double PlateScaleArcsecUmPerMm = 206.265;
        private static readonly TimeSpan GuidePublishInterval = TimeSpan.FromSeconds(1);

        private readonly NinaMediators nina;
        private readonly MqttLink mqtt;
        private readonly Discovery topics;
        private readonly int previewMaxWidth;
        private readonly MeridianFlipTracker flip;
        private readonly AutofocusTracker autofocus;

        private TelescopeInfo lastMount;
        private GuiderInfo lastGuider;
        private double guidePixelScale = double.NaN;
        private bool guiding;
        private double lastRaError = double.NaN, lastDecError = double.NaN;
        private DateTime lastGuidePublish = DateTime.MinValue;

        public TelemetryService(NinaMediators nina, MqttLink mqtt, Discovery topics, int previewMaxWidth, Func<double> minutesAfterMeridian) {
            this.nina = nina;
            this.mqtt = mqtt;
            this.topics = topics;
            this.previewMaxWidth = previewMaxWidth;
            flip = new MeridianFlipTracker(minutesAfterMeridian);
            autofocus = new AutofocusTracker(state => Guard("autofocus", () => Send(topics.AutofocusTopic, state)));

            nina.Camera.RegisterConsumer(this);
            nina.Telescope.RegisterConsumer(this);
            nina.FilterWheel.RegisterConsumer(this);
            nina.Focuser.RegisterConsumer(this);
            nina.Guider.RegisterConsumer(this);
            nina.Guider.GuideEvent += OnGuideStep;
            nina.Guider.GuidingStarted += OnGuidingStarted;
            nina.Guider.GuidingStopped += OnGuidingStopped;
            nina.ImageSave.ImageSaved += OnImageSaved;
            nina.Telescope.BeforeMeridianFlip += OnBeforeFlip;
            nina.Telescope.AfterMeridianFlip += OnAfterFlip;
        }

        public bool GuiderIsPhd2 {
            get {
                var guider = lastGuider;
                return guider?.Connected == true && guider.DeviceId?.StartsWith("PHD2", StringComparison.OrdinalIgnoreCase) == true;
            }
        }

        public void PublishGuideFrame(GuideFrame.Rendered frame) =>
            Guard("guide frame", () => mqtt.Publish(topics.GuideImageTopic, JpegEncoder.Encode(frame)));

        private void Send(string topic, JObject state) => mqtt.Publish(topic, state.ToString(Formatting.None));

        private static void Guard(string what, Action action) {
            try {
                action();
            } catch (Exception ex) {
                Log.WarnThrottled("guard-" + what, $"HA Telemetry: {what}: {ex.Message}", TimeSpan.FromMinutes(5));
            }
        }

        public void UpdateDeviceInfo(CameraInfo i) => Guard("camera", () => {
            bool on = i.Connected, cooled = on && i.CanSetTemperature;
            Send(topics.CameraTopic, new JObject {
                ["connected"] = on,
                ["name"] = on ? i.Name : null,
                ["temperature"] = on ? Num(i.Temperature, 1) : Null,
                ["setpoint"] = cooled ? Num(i.TemperatureSetPoint, 1) : Null,
                ["cooler_power"] = cooled ? Num(i.CoolerPower, 0) : Null,
                ["cooler_on"] = on && i.CoolerOn,
                ["state"] = on ? i.CameraState.ToString() : "Disconnected",
                ["gain"] = on && i.CanGetGain ? i.Gain : null,
                ["offset"] = on && i.CanSetOffset ? i.Offset : null,
                ["exposure_end"] = on && i.IsExposing ? Iso(i.ExposureEndTime) : Null,
            });
        });

        public void UpdateDeviceInfo(TelescopeInfo i) => Guard("mount", () => {
            lastMount = i;
            PublishFlip(force: false);
            Send(topics.MountTopic, i.Connected ? MountState(i) : DisconnectedMount());
        });

        private static JObject DisconnectedMount() => new() {
            ["connected"] = false,
            ["summary"] = "Disconnected",
            ["attributes"] = new JObject(),
            ["ra_hours"] = null,
            ["dec_deg"] = null,
            ["alt_deg"] = null,
            ["az_deg"] = null,
            ["pier_side"] = null,
            ["time_to_flip_min"] = null,
            ["tracking"] = false,
            ["slewing"] = false,
            ["at_park"] = false,
        };

        private static JObject MountState(TelescopeInfo i) {
            var pier = i.SideOfPier switch { PierSide.pierEast => "East", PierSide.pierWest => "West", _ => "Unknown" };
            return new JObject {
                ["connected"] = true,
                ["ra_hours"] = Num(i.RightAscension, 4),
                ["dec_deg"] = Num(i.Declination, 3),
                ["alt_deg"] = Num(i.Altitude, 2),
                ["az_deg"] = Num(i.Azimuth, 2),
                ["pier_side"] = pier,
                ["time_to_flip_min"] = Num(i.TimeToMeridianFlip * 60, 1),
                ["tracking"] = i.TrackingEnabled,
                ["slewing"] = i.Slewing,
                ["at_park"] = i.AtPark,
                ["summary"] = i.AtPark ? "Parked" : $"Alt {i.Altitude:0.0}° Az {i.Azimuth:0.0}°",
                ["attributes"] = new JObject {
                    ["ra_hours"] = Num(i.RightAscension, 5),
                    ["dec_deg"] = Num(i.Declination, 4),
                    ["ra"] = i.RightAscensionString,
                    ["dec"] = i.DeclinationString,
                    ["alt_deg"] = Num(i.Altitude, 3),
                    ["az_deg"] = Num(i.Azimuth, 3),
                    ["lst_hours"] = Num(i.SiderealTime, 5),
                    ["hour_angle_hours"] = Num(Angles.SignedHourAngle(i.SiderealTime, i.RightAscension), 4),
                    ["hours_to_meridian"] = Num(Angles.Wrap24(i.RightAscension - i.SiderealTime), 3),
                    ["hours_to_flip"] = Num(i.TimeToMeridianFlip, 3),
                    ["pier_side"] = pier,
                    ["latitude"] = Num(i.SiteLatitude, 4),
                    ["longitude"] = Num(i.SiteLongitude, 4),
                    ["tracking"] = i.TrackingEnabled,
                    ["slewing"] = i.Slewing,
                    ["at_park"] = i.AtPark,
                    ["alignment"] = i.AlignmentMode.ToString(),
                    ["target_ra_hours"] = i.TargetCoordinates != null ? Num(i.TargetCoordinates.RA, 5) : Null,
                    ["target_dec_deg"] = i.TargetCoordinates != null ? Num(i.TargetCoordinates.Dec, 4) : Null,
                },
            };
        }

        private Task OnBeforeFlip(object sender, BeforeMeridianFlipEventArgs e) {
            Guard("flip start", () => {
                flip.Start(e?.Target?.RA ?? lastMount?.RightAscension ?? double.NaN, e?.Target?.Dec ?? lastMount?.Declination ?? double.NaN);
                Log.Info($"HA Telemetry: meridian flip started ({flip.TargetDescription})");
                PublishFlip(force: true);
            });
            return Task.CompletedTask;
        }

        private Task OnAfterFlip(object sender, AfterMeridianFlipEventArgs e) {
            Guard("flip end", () => {
                bool success = e?.Success ?? false;
                flip.Finish(success);
                Log.Info($"HA Telemetry: meridian flip finished, success={success}");
                PublishFlip(force: true);
            });
            return Task.CompletedTask;
        }

        private void PublishFlip(bool force) {
            var state = flip.Snapshot(lastMount, force);
            if (state != null) Send(topics.FlipTopic, state);
        }

        public void UpdateDeviceInfo(GuiderInfo i) {
            lastGuider = i;
            if (i.Connected && i.PixelScale > 0) guidePixelScale = i.PixelScale;
            PublishGuider();
        }

        private void OnGuideStep(object sender, IGuideStep step) {
            if (!double.IsNaN(guidePixelScale)) {
                lastRaError = step.RADistanceRaw * guidePixelScale;
                lastDecError = step.DECDistanceRaw * guidePixelScale;
            }
            if (DateTime.UtcNow - lastGuidePublish >= GuidePublishInterval) PublishGuider();
        }

        private Task OnGuidingStarted(object sender, EventArgs e) {
            guiding = true;
            PublishGuider();
            return Task.CompletedTask;
        }

        private Task OnGuidingStopped(object sender, EventArgs e) {
            guiding = false;
            lastRaError = lastDecError = double.NaN;
            PublishGuider();
            return Task.CompletedTask;
        }

        private void PublishGuider() => Guard("guider", () => {
            lastGuidePublish = DateTime.UtcNow;
            var i = lastGuider;
            bool connected = i?.Connected == true, active = connected && guiding;
            var rms = connected ? i.RMSError : null;
            Send(topics.GuiderTopic, new JObject {
                ["connected"] = connected,
                ["guiding"] = active,
                ["rms_total"] = rms != null ? Num(rms.Total.Arcseconds) : Null,
                ["rms_ra"] = rms != null ? Num(rms.RA.Arcseconds) : Null,
                ["rms_dec"] = rms != null ? Num(rms.Dec.Arcseconds) : Null,
                ["ra_error"] = active ? Num(lastRaError) : Null,
                ["dec_error"] = active ? Num(lastDecError) : Null,
                ["pixel_scale"] = Num(guidePixelScale, 3),
            });
        });

        public void UpdateDeviceInfo(FocuserInfo i) => Guard("focuser", () => Send(topics.FocuserTopic, new JObject {
            ["connected"] = i.Connected,
            ["position"] = i.Connected ? i.Position : null,
            ["temperature"] = i.Connected ? Num(i.Temperature, 1) : Null,
            ["moving"] = i.Connected && i.IsMoving,
        }));

        public void UpdateUserFocused(FocuserInfo info) { }

        public void AutoFocusRunStarting() => Guard("autofocus", autofocus.RunStarting);

        public void NewAutoFocusPoint(DataPoint point) => Guard("autofocus", () => autofocus.AddPoint(point));

        public void UpdateEndAutoFocusRun(AutoFocusInfo info) => Guard("autofocus", () => autofocus.RunEnded(info));

        public void UpdateDeviceInfo(FilterWheelInfo i) => Guard("filterwheel", () => Send(topics.FilterWheelTopic, new JObject {
            ["connected"] = i.Connected,
            ["filter"] = i.Connected ? (i.IsMoving ? "Moving" : i.SelectedFilter?.Name) : null,
        }));

        private void OnImageSaved(object sender, ImageSavedEventArgs e) {
            Guard("image", () => Send(topics.ImageTopic, ImageState(e)));
            PublishPreview(e);
        }

        private static JObject ImageState(ImageSavedEventArgs e) {
            var meta = e.MetaData;
            var type = meta?.Image?.ImageType ?? "";
            var stars = type.Equals("LIGHT", StringComparison.OrdinalIgnoreCase) ? e.StarDetectionAnalysis : null;
            double pixelSize = meta?.Camera?.PixelSize ?? double.NaN, focalLength = meta?.Telescope?.FocalLength ?? double.NaN;
            int binning = Math.Max(meta?.Camera?.BinX ?? 1, 1);
            double scale = pixelSize > 0 && focalLength > 0 ? PlateScaleArcsecUmPerMm * pixelSize * binning / focalLength : double.NaN;
            var rms = meta?.Image?.RecordedRMS;
            return new JObject {
                ["hfr"] = stars != null ? Num(stars.HFR, 3) : Null,
                ["hfr_arcsec"] = stars != null ? Num(stars.HFR * scale, 3) : Null,
                ["image_scale"] = Num(scale, 4),
                ["stars"] = stars?.DetectedStars,
                ["filter"] = e.Filter,
                ["target"] = string.IsNullOrEmpty(meta?.Target?.Name) ? null : meta.Target.Name,
                ["saved_at"] = DateTime.UtcNow.ToString("o"),
                ["attributes"] = new JObject {
                    ["image_type"] = type,
                    ["image_scale"] = Num(scale, 4),
                    ["pixel_size_um"] = Num(pixelSize, 2),
                    ["focal_length_mm"] = Num(focalLength, 1),
                    ["binning"] = meta?.Camera?.Binning,
                    ["exposure_s"] = Num(e.Duration, 2),
                    ["hfr_stdev"] = e.StarDetectionAnalysis != null ? Num(e.StarDetectionAnalysis.HFRStDev, 3) : Null,
                    ["mean"] = e.Statistics != null ? Num(e.Statistics.Mean, 1) : Null,
                    ["median"] = e.Statistics != null ? Num(e.Statistics.Median, 1) : Null,
                    ["file"] = e.PathToImage != null ? Path.GetFileName(e.PathToImage.LocalPath) : null,
                    ["exposure_number"] = meta?.Image?.ExposureNumber,
                    ["guide_rms_total"] = rms != null ? Num(rms.Total * rms.Scale) : Null,
                },
            };
        }

        private void PublishPreview(ImageSavedEventArgs e) {
            var image = e.Image;
            if (previewMaxWidth <= 0 || image == null) return;
            void Encode() => Guard("preview", () => mqtt.Publish(topics.PreviewTopic, JpegEncoder.Encode(image, previewMaxWidth)));
            if (image.IsFrozen) _ = Task.Run(Encode);
            else image.Dispatcher.BeginInvoke(Encode);
        }

        public void Dispose() {
            autofocus.Dispose();
            nina.ImageSave.ImageSaved -= OnImageSaved;
            nina.Telescope.BeforeMeridianFlip -= OnBeforeFlip;
            nina.Telescope.AfterMeridianFlip -= OnAfterFlip;
            nina.Guider.GuideEvent -= OnGuideStep;
            nina.Guider.GuidingStarted -= OnGuidingStarted;
            nina.Guider.GuidingStopped -= OnGuidingStopped;
            nina.Camera.RemoveConsumer(this);
            nina.Telescope.RemoveConsumer(this);
            nina.FilterWheel.RemoveConsumer(this);
            nina.Focuser.RemoveConsumer(this);
            nina.Guider.RemoveConsumer(this);
        }
    }
}
