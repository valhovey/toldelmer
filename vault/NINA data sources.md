# NINA data sources

Where each value in [[HA Telemetry plugin]] comes from. NINA's online plugin docs return 404, so the reference is the decompiled `NINA.*` 3.2.0.9001 assemblies (see [[Build and test]] for `ilspycmd`).

- **Equipment state:** `I{Camera,Telescope,FilterWheel,Focuser,Guider}Consumer.UpdateDeviceInfo(…Info)` after `mediator.RegisterConsumer(this)`. The mediators push every few seconds whether or not a sequence runs, so no sequence instruction is needed.
- **Guiding:** `IGuiderMediator.GuideEvent` gives an `IGuideStep` with `RADistanceRaw`/`DECDistanceRaw` in **pixels**. Multiply by `GuiderInfo.PixelScale` for arcseconds. RMS is `GuiderInfo.RMSError.{RA,Dec,Total}.Arcseconds`. Guider updates are throttled to 1 per second.
- **Which guider:** `GuiderInfo.DeviceId` (live) and `GuiderSettings.GuiderName` (profile) both hold the guider's `IDevice.Id`. NINA's PHD2 driver is `"PHD2_Single"`; others are `No_Guider`, `Direct_Guider`, `MetaGuide`, `SkyGuard_Guider`, MGEN… This gates [[PHD2 guide frame]].
- **Images:** `IImageSaveMediator.ImageSaved` gives `StarDetectionAnalysis.HFR/HFRStDev/DetectedStars`, `Filter`, `Duration`, `Statistics`, `MetaData.Image.{ImageType,ExposureNumber,RecordedRMS}`, `MetaData.Target.Name`, and `Image`, a rendered and stretched `BitmapSource` that becomes the preview. HFR and stars are published only for `LIGHT` frames; flats and darks would wreck the chart.
- **Image scale:** `206.265 × MetaData.Camera.PixelSize (µm) × BinX / MetaData.Telescope.FocalLength (mm)` in ″/px, which gives `hfr_arcsec`.
- **Preview threading:** `e.Image` belongs to the UI thread unless frozen. Encode it on its `Dispatcher` if it isn't frozen, otherwise on the thread pool.
- **Mount:** `TelescopeInfo.TimeToMeridianFlip` is in **hours** and is NaN while not tracking. Hour angle = `SiderealTime − RightAscension`, wrapped to ±12 h. Site lat/lon go into the pointing attributes for the [[Dashboard cards|mount card]].
- **Meridian flip:** `ITelescopeMediator.BeforeMeridianFlip`/`AfterMeridianFlip`. See [[Meridian flip tracking]].
- **Autofocus:** no mediator event. `IFocuserConsumer` has `AutoFocusRunStarting()`, `NewAutoFocusPoint(OxyPlot.DataPoint)` and `UpdateEndAutoFocusRun(AutoFocusInfo)` (`AutoFocusInfo` is in `NINA.Equipment.Interfaces.Mediator`). See [[Autofocus telemetry]].
