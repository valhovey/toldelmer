# Local environment

What a machine needs to build and check [[HA Telemetry plugin]]. The user-facing version is the README's "Set up a dev environment" section.

- **.NET 8 SDK** (any 8.0.x; a newer SDK can also build `net8.0`). No `global.json`, deliberately, so a machine with only a newer SDK still builds.
  - Windows: `winget install Microsoft.DotNet.SDK.8`.
  - macOS/Linux: `dotnet-install.sh --channel 8.0` into `~/.dotnet`, then `DOTNET_ROOT=$HOME/.dotnet` and `PATH=$HOME/.dotnet:$PATH`. The author's Mac uses this (SDK 8.0.427 as of 2026-11-09), so `dotnet` is not on PATH in a fresh non-login shell; `scripts/check.sh` handles that ([[Pre-commit hook]]).
- **No NINA install needed.** `NINA.Plugin` comes from nuget.org with `ExcludeAssets=runtime` ([[Plugin loading]]).
- `git config core.hooksPath .githooks` to turn on the [[Pre-commit hook]].
- Optional: `uv` for `dashboard/push_resource.py`, Node.js for `node --check` on the cards ([[Dashboard cards]]), `ilspycmd` for reading NINA's API ([[Build and test]]).
