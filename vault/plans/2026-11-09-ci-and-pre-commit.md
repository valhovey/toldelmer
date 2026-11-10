---
created: 2026-11-09T20:08-07:00
updated: 2026-11-09T20:20-07:00
status: done
---

# CI build and pre-commit hook

Build the plugin on GitHub Actions with a README status badge, block commits whose build fails, and document local setup ([[Continuous integration]], [[Pre-commit hook]], [[Local environment]]).

## Plan
1. One script, `scripts/check.sh`, is the single definition of "the build passes": restore and build `plugin/` (Release), `tools/DiscoveryDump`, `tools/OfflineHarness`, then run DiscoveryDump as a smoke test. It finds `dotnet` on PATH or in `~/.dotnet`.
2. `.github/workflows/build.yml`: ubuntu-latest, `actions/setup-dotnet` 8.0.x, runs `scripts/check.sh`, uploads `dist/HA Telemetry/` as an artifact. Triggers: push and PR to `main`, manual.
3. `.githooks/pre-commit` calls `scripts/check.sh`; enabled per clone with `git config core.hooksPath .githooks`. `.gitattributes` keeps both scripts LF.
4. README: badge, a "Set up a dev environment" section; vault pages for CI, hook and local environment.

## Work log
- 2026-11-09 20:10 — wrote `scripts/check.sh`, `.githooks/pre-commit`, `.gitattributes`, `.github/workflows/build.yml` — `check: OK` locally in ~3 s.
- 2026-11-09 20:12 — added `NoWarn NU1701` to the csproj; the warnings were expected noise in hook output.
- 2026-11-09 20:13 — ran the check on an rsynced copy with an empty `NUGET_PACKAGES` to mimic CI's cold restore — OK.
- 2026-11-09 20:15 — `git config core.hooksPath .githooks`; appended a syntax error to `plugin/Log.cs` and committed — refused with exit 1; reverted. Hook passes on the clean tree.
- 2026-11-09 20:20 — README badge (repo `valhovey/toldelmer` from `origin`) and dev-environment section; vault pages written. Not committed or pushed; the workflow has not yet run on GitHub.
