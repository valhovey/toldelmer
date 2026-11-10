# Meridian flip tracking

`Telemetry/MeridianFlipTracker.cs` feeds `nina/<node>/flip` (entities `meridian_flip`, `flip_phase`, `flip_at`; see [[Discovery schema]]).

- NINA raises `BeforeMeridianFlip` at the start of its whole flip workflow and `AfterMeridianFlip` (with `Success`) at the end ([[NINA data sources]]). The target comes from the event args, falling back to the last mount RA/Dec.
- **Phase**, re-derived on every mount update while a flip is active: tracking off → `Waiting for meridian`; slewing → `Flipping`; tracking again after a stop → `Recentering`; otherwise `Starting`. Afterwards `Done`/`Failed`; before any flip, `Idle`. When idle, it publishes only when the phase changes.
- **flip_at** = now + (`MinutesAfterMeridian`/60 − target hour angle) / 1.0027379, the sidereal rate. That is when NINA's meridian flip trigger fires.
- The whole state object is duplicated into `attributes`, so one sensor carries everything for a dashboard.
