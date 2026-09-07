---
description: Continue a Squad CRM story from the current working tree — Fast Lane workflow.
---

Continue one in-flight Squad CRM story directly, from the current working
tree, on the **Fast Lane** workflow. Read
[next-story.md](.claude/commands/next-story.md) for the shared rules (main-
agent execution, hard Squad Kit gate, YAGNI, Decision Gate, verification,
Fast-Lane publication, merge approval, token/speed rules) — this command only
changes the entry point.

`$ARGUMENTS` = the issue id (e.g. `CRM-106`), optionally `--dry-run` (report
what remains; no Linear write, no file write, no git write). Refuse to proceed
without an explicit issue id — there is no auto-selection here.

## Do not

- restart the story or redo discovery/planning from scratch;
- regenerate an existing valid plan (it remains authoritative);
- repeat implementation work already done — preserve already-valid work;
- rerun verification already proven for untouched code;
- spawn subagents merely because execution is being resumed — zero subagents
  by default, per `/next-story`.

## Step 1 — Orient (read once)

Read, once: the active Linear issue (status, description, AC/BR, blockers),
the current git branch/status/log, and the existing Squad Kit artifacts for
this story. Do not re-fetch or re-read anything already established earlier in
this session; preserve valid evidence from earlier sessions.

## Step 2 — Squad Kit gate + remaining work

The hard Squad Kit gate still applies (`/next-story` Step 4). Reuse a valid
intake/plan as-is. If either artifact is missing or invalid, create or
reconcile it **before** making any further application-code change.

From the working tree and the plan, determine concretely what is done and what
is not — per file/behavior, not a vague guess — using the plan's task list as
the checklist.

If reconciliation reveals the story is actually already complete (implemented
and verified) or the state is contradictory, stop and report instead of
proceeding.

Stop here and report if invoked with `--dry-run`.

## Step 3 — Continue implementation

Move the issue to **In Progress** if it isn't already. Implement only the
remaining plan items, following `/next-story` Step 5 (Decision Gate) and Step
6 (YAGNI) unchanged.

## Step 4 — Verify what changed

Run `/next-story` Step 7 verification, scoped to this session's changes plus
anything those changes could have affected, including the required formatting
gates. Do not re-run checks unaffected by these changes; do not re-verify
untouched work already evidenced as passing.

## Step 5 — Continue automatically to PR

Run `/next-story` Step 8 (pre-publication check) and Step 9 (Fast-Lane
publication: stage → staged-diff review → commit → push → open/update PR →
Linear link → CI) automatically. No publication approval is required. Handle
CI failures owned by this story without asking.

Stop **only** at a genuine Decision Gate or at the `MERGE APPROVAL REQUIRED`
report (`/next-story` Step 10). Never merge the PR, and never start another
story automatically.
