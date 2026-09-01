---
description: Continue a Squad CRM story from the current working tree — Feature Velocity workflow.
---

Continue one in-flight Squad CRM story directly, from the current working
tree, on the **Feature Velocity** workflow. Read
[next-story.md](.claude/commands/next-story.md) for the shared rules (main-
agent execution, YAGNI, Decision Gate, verification, publication safety) — this
command only changes the entry point.

`$ARGUMENTS` = the issue id (e.g. `CRM-106`), optionally `--dry-run` (report
what remains; no Linear write, no file write, no git write). Refuse to proceed
without an explicit issue id — there is no auto-selection here.

## Do not

- restart discovery from scratch;
- regenerate an existing plan (if one exists, it remains authoritative);
- repeat implementation work already done;
- rerun passing tests/checks unaffected by subsequent changes;
- spawn subagents merely because execution is being resumed — this is still
  main-agent execution by default, per `/next-story`.

## Step 1 — Orient

Read, once: the Linear issue (status, description, AC/BR, blockers), the
current git branch/status/log, and any existing `.squad/plans/` file for this
story. Do not re-fetch or re-read anything already established earlier in this
session.

## Step 2 — Determine what remains

Squad Kit artifacts are mandatory (`CLAUDE.md` §4). If the story has no
intake/plan yet, create the minimum complete one now (per `/next-story` Step
3) before continuing — do not treat resuming as an excuse to skip it. From the
working tree and the plan, determine concretely what is done and what is not
— per file/behavior, not a vague guess — using the plan's task list as the
checklist.

If reconciliation reveals the story is actually already complete (implemented
and effectively verified) or the state is contradictory, stop and report
instead of proceeding.

Stop here and report if invoked with `--dry-run`.

## Step 3 — Continue

Move the issue to **In Progress** if it isn't already. Implement only the
remaining work, following `/next-story` Step 4 (Decision Gate) and Step 5
(YAGNI) unchanged.

## Step 4 — Verify

Run `/next-story` Step 6 verification, scoped to what changed plus anything
the changes could have affected. Do not re-run checks unaffected by this
session's changes; do not re-verify work you did not touch and that was
already evidenced as passing.

## Step 5 — Report and publish

Same concise completion report as `/next-story` Step 7, then the same
Publication Gate (Step 8) — explicit approval required before any commit,
push, PR or Linear completion write. **STOP after this story** — never start
another automatically.
