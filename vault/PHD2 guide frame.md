# PHD2 guide frame

The optional `guide_frame` image entity. NINA's `IGuiderMediator` exposes no guide-camera image, so `Phd2/Phd2Client.cs` opens a **second** JSON-RPC connection to PHD2's event server (TCP 4400; 4401… for extra instances). PHD2 accepts several clients at once, and NINA keeps its own. PHD2 is never a requirement; see [[Read-only contract]].

## When it runs
1. The **PHD2 guide frame every** option is above 0 (default 10 s) and the profile's `GuiderSettings.GuiderName` starts with `PHD2` when the plugin starts. Otherwise no client is created and the entity isn't published ([[Discovery schema]]).
2. Even then, the client opens a socket only while NINA's guider is connected **and** is PHD2 (`GuiderInfo.DeviceId`, see [[NINA data sources]]). The gate is checked every 5 s. If it turns false mid-session, the session closes. The status line then shows `PHD2 Inactive`.
3. It captures only while PHD2 is `Looping`, `Guiding`, `Calibrating`, `Selected` or `LostLock`, and only while MQTT is connected ([[Offline behaviour]]).

PHD2's address is read from NINA's own guider settings (`PHD2ServerUrl`, `PHD2ServerPort`), so it is always the one NINA uses.

## Capture
`get_lock_position` → `[x, y]` or null; `save_image` → `{"filename": <FITS>}`. The client reads the file (≤ 64 MB), deletes it, then [[Guide frame rendering|renders]] it with a brass ring at the lock position and a verdigris cross at lock + the last `GuideStep` `dx/dy`. The result is ≤ 800 px wide, published as a JPEG on `nina/<node>/guide_image`.

App state is tracked from events: `AppState`, `GuideStep`, `StartGuiding`, `StartCalibration`, `LoopingExposures`, `LoopingExposuresStopped`/`GuidingStopped`, `StarLost`, `Paused`.

## Notes from the PHD2 event-server docs
- `save_image` works even when PHD2 is stopped (it returns the last frame).
- `get_star_image(size>=15)` returns a base64 16-bit crop around the star. It errors when no star is selected. Not used.
- `GuideStep` also carries `RADuration/RADirection`, `DECDuration/DECDirection` (ms), `StarMass`, `SNR`, `HFD`, `AvgDist` and `RALimited/DecLimited`. Not published yet; candidates for PHD2-style pulse bars.
