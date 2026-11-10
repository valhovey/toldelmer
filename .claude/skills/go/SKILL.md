---
name: go
description: Reason about, plan, and execute work on any project while continuously enriching a project-local Obsidian-style knowledge vault at ./vault. Runs an explore -> plan -> garden -> do -> garden loop so long-term knowledge, gotchas, and decisions accumulate in the repo instead of evaporating. Use whenever you are investigating, changing, or making decisions about this project and want the reasoning captured for next time.
---

# go — project vault + reasoning loop

You are working inside a project. All durable knowledge about *this* project — how it is
built, why it is shaped the way it is, the gotchas that bit someone, the decisions that were
made — lives in an Obsidian-style vault at the project root:

```
./vault/
```

This skill is the bridge between the live project and that vault. Every invocation runs the
same loop:

**explore → plan → garden → do → garden**

- **explore** — orient yourself in the vault and the code, then understand what is actually being asked.
- **plan** — design an approach; for anything non-trivial, persist the plan into `vault/plans/`.
- **garden** — enrich the vault with what you just understood *before* you act (capture the map).
- **do** — execute the work.
- **garden** (again) — enrich the vault with what you learned *from* acting (capture the reality).

The two garden steps are not optional bookends — they are the point. Code records *what*
changed; the vault records *why*, *what you tried*, and *what to watch out for next time*.

## How the vault is shaped

Digital-garden style. Concepts, not folders.

- **Flat file layout.** All concept notes live directly in `vault/`. No subdirectories — **with one exception:** `vault/plans/` holds the timestamped plan records described under "Persisting plans" below. Filenames mirror Obsidian link text: `Build System.md` is the target of `[[Build System]]`.
- **Pages are concepts, not journals.** The title is the noun. The body explains what it is in *this* project, its current state, and its gotchas. Never a changelog.
- **Dense interlinking.** Every page should be reachable from several others. Liberally `[[link]]` to related concepts inline as you write.
- **Stub links are encouraged.** If you mention a concept that deserves its own page but doesn't have one yet, link it anyway (`[[Deployment]]`). The dangling link is a TODO for the next time the topic comes up.
- **`README.md` is the MOC** (Map of Content) — the top-level index. Add new pages to their appropriate section.
- **No frontmatter required** on vault pages. Plain markdown. (Plan records under `vault/plans/` are the exception — they require frontmatter, see below.)

## What to do on every invocation

### 0. Bootstrap the vault if absent

If `vault/` does not exist yet (this skill was just dropped into a fresh project), create the
minimal scaffold before doing anything else:

- `vault/README.md` — an empty MOC with a one-line description of the project and a few section headers to grow into.
- `vault/plans/README.md` — a plans index with a single header line and the format `- YYYY-MM-DD — [Title](file.md) — status`.

Then proceed. Do this quietly; it is setup, not the answer.

### 1. Explore — orient

Always begin by reading `vault/README.md`. It is small and tells you what already exists. From
the MOC, identify the 1–5 pages most relevant to the request and read those before forming an
answer. Follow `[[wikilinks]]` outward to traverse from a partial match to the real thing.

Then look at the code/artifacts the request actually touches. The vault gives you the map; the
repo is the territory.

### 2. Verify before relying

Vault entries go stale (refactors, dependency bumps, renamed files, changed config). Before
quoting a fact that the user is about to act on, sanity-check it against reality — read the
current file, run the test, check the actual config value, inspect the real output. **If reality
disagrees with the vault, trust reality** and queue the page for an update.

### 3. Plan — design the approach

For anything beyond a trivial one-liner, design an approach before touching anything, and for
non-trivial work **persist the plan into the vault** (see "Persisting plans" below). Prefer
reusing existing structure and utilities the exploration surfaced over inventing new ones.

### 4. Garden — enrich before acting

Once you understand the shape of the work, capture that understanding in the vault *now*, while
it is fresh — a new concept page, a filled gap in an existing one, a corrected stale fact. This
is cheap insurance: if the "do" step is interrupted, the map still improved.

### 5. Do — execute

Carry out the work. Keep the plan's work log current as you go (see below).

### 6. Garden — enrich after acting — always

This is the most important step. After the work, update the vault to reflect what was actually
learned:

- **New concept came up?** Create a page for it. Even a 3-line stub with a `[[link]]` or two is valuable — it gives the topic structure for next time.
- **Existing page was incomplete?** Add the new detail. Cross-link to related pages.
- **Existing page was wrong or stale?** Edit it. Do not preserve outdated information for history — this is a wiki, not a log. Replace, don't append "Update:" sections.
- **You discovered a gotcha** (something that bit you, or would bite a future you)? Either give it its own page (`Foo gotcha.md`) or add a `## Gotchas` section to the relevant concept page.
- **You linked to a stub that now has real content?** Promote it: write the page.
- **Add reciprocal links.** If `A.md` links to `[[B]]`, then `B.md` should generally link back to `[[A]]`. Density beats hierarchy.
- **Update `README.md`** when you add a new page — one line under the appropriate section.

If you changed anything in the vault during a turn, tell the user briefly at the end
("Updated `Build System.md`, created `Caching gotcha.md`"). Don't recite diffs.

## Style rules for vault pages

- **Title line:** `# Page Name` matching the filename.
- **Lead sentence:** what this concept *is in this project* (not the generic textbook definition).
- **Use sections** (`## Current state`, `## Gotchas`, `## Related`, `## Commands`) when content warrants them, but don't force them on tiny pages.
- **Prefer bullet lists** with concrete values (paths, module names, version numbers, flags, IDs) over prose.
- **Inline `[[wikilinks]]`** densely. A page with only a "Related" section at the bottom is too sparse — link as you write.
- **Be honest about uncertainty.** If a fact wasn't verified this session, mark it `(as of YYYY-MM-DD)`. If something is a guess, say so.
- **No emoji**, no decorative headings, no marketing tone.
- **Short is fine.** A 5-line page with good links beats a 50-line page that nobody reads.

## What NOT to write in the vault

- Generic documentation any web search or the framework's own docs would give. The vault is about *this project*.
- Transcripts of conversations or blow-by-blow task progress. That belongs in the chat (or a plan's work log), not concept pages.
- Secrets, tokens, passwords, private keys, connection strings.
- Time-bound TODOs ("fix this by Friday"). Describe the *current state*; if a fix is pending, note it as a gotcha or open question on the concept page.

## Private and large files

The vault, plans and code are tracked and may be pushed or shared, so anything written there
leaves the machine.

- **Never commit personal setup.** That covers broker addresses, hostnames, IP addresses, SSH keys, passwords, tokens, Home Assistant URLs and site coordinates. Use placeholders (`<broker-host>`, `nina_<hostname>`) in docs and examples.
- **Keep test fixtures out of git.** Captured FITS frames, harness output and build output are ignored by `.gitignore` (`*.fits`, `*.ppm`, `dist/`, `out/`). Pass fixtures in by path.
- **Ask before staging a new kind of file** if you are unsure whether it is private.
- **Stop if private data reaches a commit.** Tell the user right away. Rewriting history or force-pushing falls under the irreversible-actions rule below, and anything already pushed should be treated as exposed.

## Project ground rules

- **The plugin is strictly read-only towards NINA and the equipment.** It registers consumers and event handlers and reads files. It never calls a mediator method that moves, connects or configures a device, and it never accepts commands over MQTT. Keep it that way; see `[[Read-only contract]]` in the vault.
- **PHD2 is optional.** Nothing may assume PHD2 is installed or running. See `[[PHD2 guide frame]]`.
- **Never block or throw into NINA.** Mediator callbacks and event handlers return at once; network work happens on background tasks. See `[[Offline behaviour]]`.
- **No comments in the code.** The reasoning lives in the vault. When you would write a comment, write or extend a vault page instead.
- **Build and verify** with the commands on `[[Build and test]]`. The offline harness must pass after any change to the network or image code.
- **Never restart NINA or overwrite a loaded plugin DLL on someone's imaging PC** without their explicit go-ahead. A restart interrupts imaging.

## Filename conventions

- Spaces in filenames are fine and expected (they match Obsidian link syntax).
- Use Title Case for concept names: `Build System.md`, `Data Model.md`.
- For gotcha pages, suffix `gotcha`: `flaky-test gotcha.md`.
- Only put a version in a title when disambiguation requires it.

## Persisting plans

When you produce a real plan for non-trivial work, persist it into the vault so future agents
can see what was proposed and what actually got done. If your harness has an ephemeral plan file
of its own, treat the vault copy as the durable record.

### Where it lives

- Path: `vault/plans/<YYYY-MM-DD>-<short-slug>.md`. The date is when the plan was first written. The slug is short, hyphenated, descriptive (`add-auth-caching`, not `some-cool-idea`).
- Index: `vault/plans/README.md` — chronological, one line per plan: `- YYYY-MM-DD — [Title](filename.md) — status`.

### Frontmatter (required on plan records)

```
---
created: YYYY-MM-DDTHH:MM±TZ
updated: YYYY-MM-DDTHH:MM±TZ
status: proposed | in-progress | done | abandoned
---
```

- `created` is set once, never changed. `updated` is bumped on every edit (including work-log appends).
- `status` transitions: `proposed` → `in-progress` (first work-log entry) → `done` (verification passes) or `abandoned` (redirected/cancelled). Keep the index in sync.

### Work log (required)

Every plan record ends with a `## Work log` section. As work executes, append one bullet per
non-trivial action — successes *and* failures. The log is for future-you, not a marketing summary.

```
- YYYY-MM-DD HH:MM — what was done — outcome / next
```

### Backfill rule

If you find prior plans referenced but not captured in `vault/plans/`, create stubs with
`status: unknown` and a single work-log line noting that earlier progress wasn't recorded.

### When NOT to persist a plan

- Trivial single-edit tasks where no real plan existed.
- One-off recurring/scheduled instructions that aren't project work.

## Irreversible and privileged actions

Some actions are hard to undo (deletes, force-pushes, migrations, prod deploys) or need
elevated privileges or external side effects. Do not assume authorization. Stop, state exactly
what you are about to run and why, and confirm with the user — or hand off the command for them
to run — before proceeding. Approval in one context doesn't extend to the next.

## Failure modes to avoid

- **Don't answer before reading the vault.** Even a quick `README.md` skim reframes generic advice into something project-specific.
- **Don't trust the vault blindly.** If a fact is load-bearing for an action, verify it against reality first.
- **Don't leave the vault unchanged after a non-trivial turn.** Either you confirmed existing info (no edit needed), or you learned something new (edit needed). Pure no-op turns should be rare.
- **Don't create duplicate pages.** Check `README.md` and list `vault/` for an existing page before creating one. If two pages cover the same concept, merge them.
- **Don't reorganize aggressively.** The vault belongs to the project's maintainers; make small additive edits each turn, not big restructures.
