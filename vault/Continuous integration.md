# Continuous integration

GitHub Actions workflow `.github/workflows/build.yml` (name `Build`) that runs the same `scripts/check.sh` as the [[Pre-commit hook]]. The README badge points at it.

- Runs on `ubuntu-latest` with `actions/setup-dotnet@v4`, `dotnet-version: 8.0.x`. Linux works because of `EnableWindowsTargeting` ([[Build and test]]); it is also the cheapest runner.
- Triggers: push and pull request to `main`, plus `workflow_dispatch`.
- Uploads `dist/HA Telemetry/` as artifact `HA-Telemetry` (`if-no-files-found: error`). That artifact is an installable plugin folder ([[Plugin loading]]), not a release.
- Restore pulls `NINA.Plugin 3.2.0.9001` and MQTTnet from nuget.org; there is no `nuget.config` and no NuGet cache step (no lock file). A clean restore with an empty `NUGET_PACKAGES` was verified locally on 2026-11-09.
- The OfflineHarness is built but not run: it needs a PHD2 FITS frame, and fixtures stay out of git.

## Gotchas
- The badge URL hard-codes the GitHub repo from `origin` (`valhovey/toldelmer` as of 2026-11-09). Renaming or moving the repo breaks the badge.
- Not yet observed running on GitHub as of 2026-11-09; only the script was verified locally.
- A Windows leg would catch path or casing issues Linux hides; not added because the build has none so far.
