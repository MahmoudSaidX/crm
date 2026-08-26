---
description: Safely continue a story already classified PARTIALLY_IMPLEMENTED or IMPLEMENTED_NOT_REVIEWED, reusing its approved Squad Kit plan.
---

Resume one in-flight Squad CRM story. This is the sanctioned continuation path
out of the `/next-story` Reconciliation Gate — it does **not** restate that
workflow. Read [next-story.md](.claude/commands/next-story.md) and follow its
phases by reference; this command only changes the entry conditions and the
starting point.

`$ARGUMENTS` = the issue id (e.g. `CRM-106`), optionally `--dry-run`
(reconciliation + task matrix only; no Linear write, no file write, no git write).
Refuse to proceed without an explicit issue id — there is no auto-selection here.

## Preconditions

Invoke only after `/next-story` (or an equivalent read-only reconciliation)
classified this story **C. PARTIALLY_IMPLEMENTED** or **D.
IMPLEMENTED_NOT_REVIEWED**, and the user explicitly approved continuing it.
An **approved Squad Kit plan is required**. If no plan exists, stop and say so:
never generate or regenerate a story intake or a plan from here — that is
`/next-story` Phases 4–5, and regenerating an approved plan needs the user's
explicit go-ahead.

The separation of concerns is fixed: Linear MCP is the live tracker, the Squad
Kit intake is the planning snapshot, the Squad Kit plan is the approved
implementation plan, Git is the implementation evidence, `/next-story` and this
command are orchestration, Superpowers is the implementation methodology.
Do not modify `.squad/config.yaml`.

`/next-story`'s **Token discipline** section applies here unchanged, including
its rule that no gate is ever skipped or shortened to save tokens.

## Step 1 — Read the tracker

Linear MCP `get_issue` with `includeRelations: true`: status and history,
description, Acceptance Criteria, Business Rules, `blockedBy` and `blocks` with
their statuses, attachments/PRs.

Fetch this **once** here and reuse it through Steps 2–4; refresh only
immediately before a tracker write (Step 5) or the Publication Gate.

## Step 2 — Reconcile again (read-only, before any modification)

Re-run the `/next-story` Phase 2 evidence sweep in full — Linear state, git
history, current branch, working tree, Squad Kit intake, approved plan,
implementation evidence. Evidence from a previous invocation is stale; gather it
fresh. By the entry conditions, prior work exists here, so Phase 2's deep
implementation inspection is always required — but it is the *same single sweep*
that produces the Step 3 matrix: read the approved plan completely once, verify
each named artifact with targeted `grep`/`test -f`/`git log --stat` rather than
whole-file dumps, and do not repeat the sweep per step. If the class has changed to **E. COMPLETED** or **F. CONFLICTED**, stop
and report instead of resuming.

## Step 3 — Task matrix

Walk the approved plan task by task and emit:

```
Plan task | Status | Evidence
```

Status is exactly one of **COMPLETE**, **PARTIAL**, **NOT_STARTED**,
**CONFLICTED**. Evidence must be concrete — a file path, a symbol, a commit
sha, or the specific absence observed. Vague status without evidence is not an
acceptable row.

**Do not redo COMPLETE work merely because it predates this invocation.**

Build this matrix **once**, from the Step 2 sweep. Treat COMPLETE rows as
trusted evidence for the rest of the run: do not re-inspect their implementation
while executing the remaining tasks. Revisit COMPLETE work only if Step 7
verification or the Step 8 review exposes a regression or contradiction — and
then correct the matrix row with fresh evidence.

## Step 4 — Conflict review of the existing partial work

Before continuing, review what already exists against the *currently approved*
plan and against `docs/adr/` and `CLAUDE.md`: does the committed code contradict
the approved architecture (wrong boundary, inverted dependency, persistence
ownership elsewhere, a contract the plan does not sanction)?

If yes: **STOP** with the `DECISION REQUIRED` block from `/next-story` Phase 6.
Never silently rewrite committed architecture — reconciling committed code with
an approved plan is the user's decision.

## Step 5 — Linear

If reconciliation is safe and this is not a dry run, move the issue to
**In Progress** if it is not already. Touch no other issue.

## Step 6 — Continue the remainder

Implement only the PARTIAL and NOT_STARTED tasks, in plan order, via the
Superpowers workflow named in `/next-story` Phase 7. Work from the current
task's plan section — the plan was read in full in Step 2 and need not be
reloaded or restated. Run only the checks relevant to each changed task here;
the full verification is Step 7. Scope rules from
`/next-story` still bind: current story only, no downstream stories, no silent
scope expansion. Implementation-level defects are yours to fix autonomously;
any architectural or product question re-enters the same hard Decision Gate
(`/next-story` Phase 6) with no exceptions for work already committed.

## Step 7 — Verify the whole story

Once the implementation is stable, run — exactly once — every verification the
approved plan requires, plus the `/next-story` Phase 8 checklist, over the
**entire** story, including work that existed before this invocation.
Pre-existing code is not exempt from verification. Never claim a command passed
without having run it and read its output.

Record passes as `<command> → PASS (<key counts/evidence>)`; keep failure output
in full while debugging. After a fix, rerun the affected checks, and rerun the
full suite when the fix could affect broader behavior or to produce the final
completion evidence.

## Step 8 — Review the whole story

One fresh review — not several of the same unchanged code — of the complete
final implementation and diff (`git diff main...` plus the working tree; this is
the gate where full diffs are read) against: the Linear story, Acceptance Criteria, Business
Rules, the approved plan, ADRs, architecture boundaries, and scope exclusions.
Fix implementation-level findings autonomously and rerun the affected
verification.

## Step 9 — Completion Gate and Publication Gate

Emit the same 13-point completion report as `/next-story` Phase 10, under its
compact reporting rules (`PASS — <evidence>` for successes; detail only for
failures, deviations, architecture decisions, security concerns, technical debt,
or on request), then **STOP
before any commit, push or PR**. Publication follows `/next-story` Phase 11
unchanged: explicit approval, stage only current-story files, inspect the staged
diff, `feat/crm-<n>-<slug>` branch, no force-push, `gh pr create`, reference the
PR in Linear, move to **In Review** — never Done because a PR exists.

**STOP after one story.** Never start another automatically.
