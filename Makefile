DOTNET_HOME := $(HOME)/.dotnet
DOTNET ?= $(shell command -v dotnet 2>/dev/null)
ifeq ($(DOTNET),)
  ifneq ($(wildcard $(DOTNET_HOME)/dotnet),)
    DOTNET := $(DOTNET_HOME)/dotnet
    export DOTNET_ROOT := $(DOTNET_HOME)
  else
    DOTNET := dotnet
  endif
endif
export DOTNET_CLI_TELEMETRY_OPTOUT := 1
export DOTNET_NOLOGO := 1

FITS ?=
OUT ?= out/harness

.PHONY: build test clean

build:
	$(DOTNET) build plugin/NinaHaTelemetry.csproj -c Release -nologo

test:
	sh scripts/check.sh
ifneq ($(FITS),)
	mkdir -p "$(OUT)"
	$(DOTNET) run --project tools/OfflineHarness/OfflineHarness.csproj -c Release --no-build -- "$(FITS)" "$(OUT)"
else
	@echo "make test: OfflineHarness skipped; run make test FITS=<phd2-frame.fits> to include it"
endif

clean:
	rm -rf dist out plugin/bin plugin/obj tools/DiscoveryDump/bin tools/DiscoveryDump/obj tools/OfflineHarness/bin tools/OfflineHarness/obj
