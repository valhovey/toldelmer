# Pre-commit hook

A tracked git hook at `.githooks/pre-commit` that refuses a commit when `scripts/check.sh` fails. Same script as [[Continuous integration]], so "passes locally" and "passes on CI" mean the same thing.

- Enable once per clone: `git config core.hooksPath .githooks`. Git does not do this automatically on clone; the README says to.
- `scripts/check.sh`: builds `plugin/` (Release, stages `dist/`), `tools/DiscoveryDump`, `tools/OfflineHarness`; runs DiscoveryDump with node id `nina_ci`; checks the three staged files exist. About 3 s warm. Prints `check: OK`.
- It finds `dotnet` on PATH or falls back to `~/.dotnet` ([[Local environment]]), so it works from GUI git clients that don't load the shell profile.
- `.gitattributes` forces LF on `*.sh` and `.githooks/*` so the hook still runs from a Windows checkout (Git for Windows runs hooks under its `sh`).
- Bypass once with `git commit --no-verify`.

## Gotchas
- It builds the **working tree**, not the staged snapshot. Unstaged fixes can make a broken commit pass, and unstaged breakage can block a good one. Stashing unstaged changes inside the hook was rejected as too risky to people's work.
- It runs on every commit, including vault-only ones. Cheap enough not to filter.
