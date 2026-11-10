# Agent skills

Two Claude Code skills in `.claude/skills/` keep work on this repo documented here:

- `/go <task>`: the explore → plan → garden → do → garden loop over this vault. It also carries the project ground rules: the [[Read-only contract]], PHD2 optional ([[PHD2 guide frame]]), never block NINA ([[Offline behaviour]]), no comments in code, and verify with [[Build and test]].
- `/build-skill <name> - <goal>`: generates a self-contained, narrowed variant of `go` (e.g. a release or review skill) in `.claude/skills/<name>/`, with the same vault discipline and ground rules.

## No comments in code
The source has no comments, by decision (2026-11-09). Anything that would have been a comment (a reason, a gotcha, an API quirk) goes on the relevant vault page instead. Names and small methods carry the "what"; the vault carries the "why".
