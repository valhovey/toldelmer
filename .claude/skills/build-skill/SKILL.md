---
name: build-skill
description: Generate a new, self-contained Claude skill that inherits the go explore -> plan -> garden -> do -> garden vault loop but is narrowed to one specific goal or type of work (e.g. code review, release cutting, incident triage). Use when the user runs `/build-skill <name> - <goal>` and wants a reusable, specialized variant of the go loop written into the project's .claude/skills/.
---

# build-skill — generate a narrowed variant of the go loop

You turn a one-line description of a recurring job into a full, self-contained skill. Every
skill you emit keeps the **explore → plan → garden → do → garden** vault loop from `go`, and
adds a `## Narrowed context` layer that specializes it for one goal.

The output is a skill someone can invoke over and over to run *that specific kind of work* with
the same knowledge-gardening discipline baked in.

## Invocation contract

```
/build-skill <name> - <goal / type of work>
```

- Split the argument on the **first** ` - ` (space-hyphen-space).
- Left of it → `<name>`: the skill's name. Slugify to lowercase-kebab for the directory and the frontmatter `name` (`Release Cut` → `release-cut`).
- Right of it → `<goal>`: a sentence describing what this skill should do and when.

Example:

```
/build-skill review - execute a code review cycle on the current staged changes and propose a plan for fixing identified problems with user input
```

→ name `review`, goal "execute a code review cycle on the current staged changes and propose a
plan for fixing identified problems with user input".

If the argument has no ` - ` separator, ask the user for the missing half (a name or a goal)
before generating anything. Don't guess a name from a bare goal, or vice versa.

## What to do

### 1. Explore
- Read `vault/README.md` (bootstrap the vault first if it's absent — same rule as `go`) to learn the project's vocabulary and conventions, so the generated skill speaks the project's language.
- Read the `go` skill (`.claude/skills/go/SKILL.md`) if present — it is your base template. If it isn't present, use the embedded loop described in this file; the generated skill must be self-contained regardless.
- Check `.claude/skills/<name>/` doesn't already exist. If it does, ask whether to overwrite or pick a new name.

### 2. Plan the specialization
From `<goal>`, work out the narrowed context the new skill needs. Answer these for the goal and
bake the answers into the generated skill:
- **Scope / entry points** — what does this skill look at first? (e.g. `git diff --staged`, the open PR, the failing job, the changelog.)
- **What "explore" means here** — the specific things to read/verify for this kind of work.
- **What "do" means here** — the concrete deliverable (a review with findings, a proposed fix plan, a cut release, a triaged incident).
- **What to garden** — which vault pages this work should create or update (e.g. a `Review Findings` page, a per-subsystem gotcha page, a `Release Checklist` page).
- **User-input points** — where the skill must pause and ask the user rather than proceed (the example's "with user input" is one of these).

### 3. Generate the skill
Write `./.claude/skills/<name>/SKILL.md` in the current project. It must be **self-contained** —
embed the whole loop, don't merely reference `go` (the generated skill has to work even if `go`
is later removed). Structure:

1. **Frontmatter** — `name: <slug>` and a `description` derived from `<goal>`, phrased as
   "do X while enriching the project vault at ./vault; use when …". The description is what makes
   the skill auto-trigger, so make it specific to the goal.
2. **Title + one-paragraph statement** of the goal and that it runs the vault loop.
3. **The loop, specialized** — the same explore → plan → garden → do → garden structure as `go`,
   but with each phase's generic instructions replaced/augmented by the goal-specific answers
   from step 2. Keep the vault mechanics verbatim: `./vault`, flat concept files, `[[wikilinks]]`,
   `README.md` as MOC, bootstrap-if-absent, and the `vault/plans/` plan-persistence rules
   (frontmatter + work log) for any non-trivial plan the skill produces.
4. **`## Narrowed context`** — the scope, entry points, deliverable, gardening targets, and
   user-input checkpoints for this goal, stated concretely.
5. **The style rules, "what NOT to write", "private and large files" rules, project ground rules and
   irreversible-actions guardrail** carried over from `go` (trim to what's relevant, but keep the vault
   discipline, the private-file rules and the project ground rules intact).

Do not put personal names, machine names, or absolute home paths in generated skills — keep them
portable, exactly like `go`.

### 4. Garden
- Add a stub page for the new skill to the project vault (e.g. `<Name> skill.md`: what it does, when to run it, what it gardens) and link it from `vault/README.md`.
- Tell the user what you created: the path `.claude/skills/<name>/SKILL.md` and how to invoke it (`/<name>`).

## Worked example — the `review` case

Input:

```
/build-skill review - execute a code review cycle on the current staged changes and propose a plan for fixing identified problems with user input
```

The generated `.claude/skills/review/SKILL.md` would:

- **Frontmatter**: `name: review`; description like "Run a code-review cycle over the staged
  diff, surface correctness/quality findings, and propose a fix plan with the user, while
  enriching the project vault. Use before committing non-trivial staged changes."
- **explore**: read `vault/README.md` and any `[[Review Findings]]` / subsystem gotcha pages;
  run `git diff --staged` and read the touched files in full for context.
- **plan**: group findings by severity; draft a fix plan.
- **garden (before)**: capture any newly-understood subsystem quirks as concept/gotcha pages.
- **do**: present findings, then **pause for user input** on which to fix and how — the fix plan
  is persisted to `vault/plans/<date>-review-fixes.md` (frontmatter + work log) since it's real work.
- **garden (after)**: record recurring problems as durable gotcha pages so the next review starts
  ahead; update `vault/README.md`.
- Carries over the style rules, "what NOT to write", the private-file rules, the project ground rules, and the irreversible-actions guardrail.

## Failure modes to avoid

- **Don't emit a skill that just says "follow go".** It must stand alone with the full loop embedded.
- **Don't drop the gardening.** The narrowed context is *added on top of* the vault loop, never a replacement for it.
- **Don't invent a name or goal** the user didn't give — ask for the missing half.
- **Don't leak personal/machine specifics** into the generated skill; keep it portable.
- **Don't overwrite an existing skill** without confirming.
