#!/bin/sh
set -eu

cd "$(dirname "$0")/.."

if ! command -v dotnet >/dev/null 2>&1; then
  if [ -x "$HOME/.dotnet/dotnet" ]; then
    export DOTNET_ROOT="$HOME/.dotnet"
    export PATH="$HOME/.dotnet:$PATH"
  else
    echo "check: dotnet not found. Install the .NET 8 SDK (see README, 'Set up a dev environment')." >&2
    exit 127
  fi
fi

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

echo "check: building plugin"
dotnet build plugin/NinaHaTelemetry.csproj -c Release -nologo -v quiet

echo "check: building tools"
dotnet build tools/DiscoveryDump/DiscoveryDump.csproj -c Release -nologo -v quiet
dotnet build tools/OfflineHarness/OfflineHarness.csproj -c Release -nologo -v quiet

echo "check: running DiscoveryDump"
dotnet run --project tools/DiscoveryDump/DiscoveryDump.csproj -c Release --no-build -- nina_ci > /dev/null

for f in NINA.HaTelemetry.dll NINA.HaTelemetry.pdb MQTTnet.dll; do
  if [ ! -f "dist/HA Telemetry/$f" ]; then
    echo "check: dist/HA Telemetry/$f missing" >&2
    exit 1
  fi
done

echo "check: OK"
