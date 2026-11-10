# Build and test

`scripts/check.sh` is the one-command gate: plugin and tools build, DiscoveryDump runs. The [[Pre-commit hook]] and [[Continuous integration]] both call it. Setup is on [[Local environment]].

## Commands
The `Makefile` wraps these: `make build` (plugin only), `make test` (`scripts/check.sh`; add `FITS=<phd2.fits>` and optionally `OUT=<dir>`, default `out/harness`, to run the OfflineHarness too), `make clean`.

```sh
export PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet      # if the SDK is user-local
cd plugin && dotnet build -c Release                          # stages ../dist/HA Telemetry/
cd tools/DiscoveryDump && dotnet run -- nina_example           # every discovery config
cd tools/OfflineHarness && dotnet run -- <phd2.fits> <out-dir> # ALL PASS / SOME CHECKS FAILED
```

- .NET 8 SDK. `EnableWindowsTargeting=true` lets the WPF project build on macOS and Linux. NU1701 warnings about NINA's .NET Framework dependencies are expected and suppressed with `NoWarn` in the csproj. The CA1416 warnings on `ProtectedData` are expected too (DPAPI is Windows-only; [[Options page]]).
- `dist/HA Telemetry/` holds exactly the installed files: `NINA.HaTelemetry.dll`, `.pdb` and `MQTTnet.dll`. Host assemblies come from `NINA.Plugin` with `ExcludeAssets=runtime` and are never shipped ([[Plugin loading]]).
- The **OfflineHarness** compiles `Log`, `Discovery`, `MqttLink`, `GuideFrame` and `Phd2Client` straight from `plugin/` (no NINA, no WPF). It needs one FITS saved by PHD2. Fixtures and its `.ppm` output are git-ignored. Re-run it after touching `MqttLink`, `Phd2*` or `GuideFrame` ([[Offline behaviour]], [[Guide frame rendering]]).
- **Refactor check:** diff `DiscoveryDump` output and the harness's `guide.ppm` against the previous version. Both should be identical unless the change is meant to alter them.

## Reading NINA's API
`ilspycmd` 8.2.0.7535 (newer versions need a newer runtime) with `DOTNET_ROLL_FORWARD=Major`, run against `~/.nuget/packages/nina.*/3.2.0.9001/lib/*/*.dll`. Results go on [[NINA data sources]].

## Gotchas
- The `Makefile` resolves `dotnet` to a full path (PATH, then `~/.dotnet/dotnet`) and calls `$(DOTNET)`. macOS's stock GNU make 3.81 looks up simple recipe commands with its *own* PATH, so `export PATH := ...` in the Makefile did not help: `make: dotnet: No such file or directory`.
- The harness exits 1 on a failed check and aborts (exit 134) on an unreadable FITS; either fails `make test`.
