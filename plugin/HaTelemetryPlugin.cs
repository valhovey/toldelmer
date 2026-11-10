using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NinaHaTelemetry.Mqtt;
using NinaHaTelemetry.Phd2;
using NinaHaTelemetry.Telemetry;

namespace NinaHaTelemetry {

    [Export(typeof(IPluginManifest))]
    public sealed class HaTelemetryPlugin : PluginBase, INotifyPropertyChanged {
        private const int GuideFrameMaxWidth = 800;
        private const string PasswordKey = "PasswordDpapi";

        private readonly IProfileService profileService;
        private readonly NinaMediators nina;
        private readonly System.Timers.Timer statusTimer = new(3000);
        private readonly SemaphoreSlim lifecycle = new(1, 1);
        private PluginOptionsAccessor options;

        private MqttLink link;
        private TelemetryService service;
        private Phd2Client phd2;

        [ImportingConstructor]
        public HaTelemetryPlugin(IProfileService profileService, ICameraMediator camera, ITelescopeMediator telescope,
                                 IFilterWheelMediator filterWheel, IFocuserMediator focuser, IGuiderMediator guider,
                                 IImageSaveMediator imageSave) {
            this.profileService = profileService;
            nina = new NinaMediators(camera, telescope, filterWheel, focuser, guider, imageSave);
            options = new PluginOptionsAccessor(profileService, Guid.Parse(Identifier));
            Log.Info = message => Logger.Info(message);
            Log.Warn = message => Logger.Warning(message);
            profileService.ProfileChanged += OnProfileChanged;
            ApplyCommand = new AsyncRelayCommand(Restart);
            RemoveEntitiesCommand = new AsyncRelayCommand(RemoveEntities);
            statusTimer.Elapsed += (_, _) => RaisePropertyChanged(nameof(Status));
        }

        public override Task Initialize() {
            statusTimer.Start();
            _ = Task.Run(Restart);
            return Task.CompletedTask;
        }

        public override async Task Teardown() {
            statusTimer.Stop();
            profileService.ProfileChanged -= OnProfileChanged;
            await Serialized(Stop);
        }

        private void OnProfileChanged(object sender, EventArgs e) {
            options = new PluginOptionsAccessor(profileService, Guid.Parse(Identifier));
            RaisePropertyChanged(string.Empty);
            _ = Task.Run(Restart);
        }

        private Task Restart() => Serialized(async () => {
            await Stop();
            if (Enabled && !string.IsNullOrWhiteSpace(Host)) {
                try {
                    Start();
                } catch (Exception ex) {
                    Logger.Error("HA Telemetry: start failed", ex);
                }
            }
            RaisePropertyChanged(nameof(Status));
        });

        private void Start() {
            var profile = profileService.ActiveProfile;
            bool guideFrame = GuideImageInterval > 0 && IsPhd2(profile.GuiderSettings.GuiderName);
            var discovery = new Discovery(DiscoveryPrefix, NodeId, $"NINA ({Environment.MachineName})", Version.ToString(), guideFrame);
            var mqtt = new MqttLink(new MqttSettings(Host.Trim(), Port, UseTls, Username, Password, NodeId), discovery);
            mqtt.Start();
            var telemetry = new TelemetryService(nina, mqtt, discovery, PreviewMaxWidth,
                                                 () => profileService.ActiveProfile.MeridianFlipSettings.MinutesAfterMeridian);
            link = mqtt;
            service = telemetry;
            Logger.Info($"HA Telemetry: publishing to {Host}:{Port} as node {NodeId}");

            if (guideFrame) {
                var guider = profile.GuiderSettings;
                phd2 = new Phd2Client(guider.PHD2ServerUrl, guider.PHD2ServerPort, TimeSpan.FromSeconds(GuideImageInterval), GuideFrameMaxWidth,
                                      isActive: () => telemetry.GuiderIsPhd2, canPublish: () => mqtt.IsConnected,
                                      publish: telemetry.PublishGuideFrame);
                phd2.Start();
            }
        }

        private async Task Serialized(Func<Task> action) {
            await lifecycle.WaitAsync();
            try { await action(); } finally { lifecycle.Release(); }
        }

        private async Task Stop() {
            if (phd2 != null) {
                try { await phd2.DisposeAsync(); } catch (Exception ex) { Logger.Warning($"HA Telemetry: PHD2 stop: {ex.Message}"); }
                phd2 = null;
            }
            if (service != null) {
                try { service.Dispose(); } catch (Exception ex) { Logger.Warning($"HA Telemetry: stop: {ex.Message}"); }
                service = null;
            }
            if (link != null) {
                await link.DisposeAsync();
                link = null;
            }
        }

        private async Task RemoveEntities() {
            if (link?.IsConnected == true) await link.RemoveDiscovery();
        }

        private static bool IsPhd2(string guiderId) => guiderId?.StartsWith("PHD2", StringComparison.OrdinalIgnoreCase) == true;

        public ICommand ApplyCommand { get; }
        public ICommand RemoveEntitiesCommand { get; }

        public string Status {
            get {
                if (!Enabled) return "Disabled";
                if (string.IsNullOrWhiteSpace(Host)) return "No broker host set";
                var current = link;
                if (current == null) return "Stopped";
                if (!current.IsConnected) return $"Connecting to {Host}:{Port}… {current.LastError}";
                var guide = phd2 == null ? "" : $"; PHD2 {phd2.AppState}";
                return $"Connected to {Host}:{Port}, node {NodeId}{guide}";
            }
        }

        public bool Enabled { get => options.GetValueBoolean(nameof(Enabled), true); set => SetBool(value); }
        public string Host { get => options.GetValueString(nameof(Host), ""); set => SetString(value); }
        public int Port { get => options.GetValueInt32(nameof(Port), 1883); set => SetInt(value); }
        public bool UseTls { get => options.GetValueBoolean(nameof(UseTls), false); set => SetBool(value); }
        public string Username { get => options.GetValueString(nameof(Username), ""); set => SetString(value); }
        public string DiscoveryPrefix { get => options.GetValueString(nameof(DiscoveryPrefix), "homeassistant"); set => SetString(value); }
        public int PreviewMaxWidth { get => options.GetValueInt32(nameof(PreviewMaxWidth), 1280); set => SetInt(Math.Max(0, value)); }
        public int GuideImageInterval { get => options.GetValueInt32(nameof(GuideImageInterval), 10); set => SetInt(Math.Max(0, value)); }

        public string NodeId {
            get {
                var stored = options.GetValueString(nameof(NodeId), "");
                return string.IsNullOrEmpty(stored) ? DefaultNodeId : stored;
            }
            set => SetString(Slug(value));
        }

        private static string DefaultNodeId => Slug("nina_" + Environment.MachineName);

        public string Password {
            get {
                var encrypted = options.GetValueString(PasswordKey, "");
                if (string.IsNullOrEmpty(encrypted)) return "";
                try {
                    var bytes = ProtectedData.Unprotect(Convert.FromBase64String(encrypted), null, DataProtectionScope.CurrentUser);
                    return Encoding.UTF8.GetString(bytes);
                } catch (Exception ex) when (ex is CryptographicException or FormatException) {
                    return "";
                }
            }
            set {
                var encrypted = string.IsNullOrEmpty(value) ? ""
                    : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));
                options.SetValueString(PasswordKey, encrypted);
                RaisePropertyChanged();
            }
        }

        private void SetString(string value, [CallerMemberName] string key = null) {
            options.SetValueString(key, value);
            RaisePropertyChanged(key);
        }

        private void SetInt(int value, [CallerMemberName] string key = null) {
            options.SetValueInt32(key, value);
            RaisePropertyChanged(key);
        }

        private void SetBool(bool value, [CallerMemberName] string key = null) {
            options.SetValueBoolean(key, value);
            RaisePropertyChanged(key);
        }

        private static string Slug(string s) => Regex.Replace((s ?? "").ToLowerInvariant(), "[^a-z0-9_]+", "_").Trim('_');

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
