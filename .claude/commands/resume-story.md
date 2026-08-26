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
its rule that no gate is ever skipped or shortened to save tokens, and its
**Proportional plans** and **Compact returns** rules.

`/next-story`'s **Phase 2c YAGNI Gate** applies here unchanged, to the remaining
implementation and to the review of what already exists.

## Model routing

`/next-story`'s **Model routing** section applies here unchanged and is the only
mechanism for model selection — slash-command `model:` frontmatter is ignored
and must never be relied on. This interactive main thread is the **CONTROLLER**:
it sequences steps, owns every stop point, preserves workflow state, receives
compact agent results, classifies, presents `DECISION REQUIRED`, requests
publication approval, and dispatches the next step. It does not perform large
repository reads, implementation, planning, debugging or repetitive verification
itself.

| Step | Routed to | Model |
|---|---|---|
| 1 — tracker read | `repo-scout` | haiku |
| 2 — reconciliation sweep | `repo-scout` | haiku |
| 3 — task-matrix evidence | `repo-scout` (controller assigns the statuses) | haiku |
| 3b — risk classification | controller | — |
| 4 — conflict review of partial work | `arch-reviewer` (ARCHITECTURE / HIGH_RISK, or an escalated question) | opus |
| 5 — Linear write | controller | — |
| 6 — implement the remainder | `story-engineer` | sonnet |
| 7 — full verification | `verify-runner` | haiku |
| 8 — independent review | `story-reviewer` | sonnet |
| 9 — report + publication | controller (after user approval) | — |

Escalation is unchanged: **Haiku → Sonnet** when code meaning must be reasoned
about, a failure cause is non-obvious, multiple behaviors interact, or findings
conflict; **Sonnet → Opus** when architecture boundaries may change, approved-plan
assumptions are challenged, multiple architectural options exist, or an ADR /
security / product / persistence / transaction / integration decision is
required. Every agent uses the `ESCALATION REQUIRED` brief from `/next-story`.
The controller forwards the brief plus the relevant anchors — investigation
already established is never redone. Gate authority is never delegated: agents
give evidence and recommendations, the controller decides whether a gate passes,
and the user decides architectural/product decisions and publication.

## Step 1 — Read the tracker

Dispatch **`repo-scout`** (haiku, read-only): Linear MCP `get_issue` with
`includeRelations: true` — status and history, description, Acceptance Criteria,
Business Rules, `blockedBy` and `blocks` with their statuses, attachments/PRs,
returned as compact structured evidence.

Fetch this **once** here and reuse it through Steps 2–4; refresh only
immediately before a tracker write (Step 5) or the Publication Gate.

## Step 2 — Reconcile again (read-only, before any modification)

Dispatch **`repo-scout`** (haiku) to re-run the `/next-story` Phase 2 evidence
sweep in full — Linear state, git
history, current branch, working tree, Squad Kit intake, approved plan,
implementation evidence. Evidence from a previous invocation is stale; gather it
fresh. By the entry conditions, prior work exists here, so Phase 2's deep
implementation inspection is always required — but it is the *same single sweep*
that produces the Step 3 matrix: read the approved plan completely once, verify
each named artifact with targeted `grep`/`test -f`/`git log --stat` rather than
whole-file dumps, and do not repeat the sweep per step. The scout reports presence and absence
only; the **controller** classifies, exactly as in `/next-story` Phase 2. If the
scout escalates because existing code's meaning must be reasoned about, route
that brief to **`story-engineer`** (sonnet) — and on to **`arch-reviewer`**
(opus) if it becomes architectural. If the class has changed to
**E. COMPLETED** or **F. CONFLICTED**, stop and report instead of resuming.

## Step 3 — Task matrix

The `repo-scout` sweep from Step 2 supplies the per-task evidence. The
**controller** assigns each status — an agent never does. Emit, task by task
through the approved plan:

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
while executing the remaining tasks, and tell `story-engineer` which tasks are
COMPLETE so it does not re-investigate them either. Revisit COMPLETE work only
if Step 7 verification or the Step 8 review exposes a regression or
contradiction — and then correct the matrix row with fresh evidence.

## Step 3b — Risk classification

The **controller** classifies this story with the same rules and the same
printed output as `/next-story` **Phase 2b** — STANDARD, ARCHITECTURE or
HIGH_RISK, escalating only on the concrete evidence Phase 2b requires — and
routes Steps 4, 6, 7 and 8
accordingly. Print:

```
RISK: STANDARD | ARCHITECTURE | HIGH_RISK
Reason: <one concise sentence>
```

## Step 4 — Conflict review of the existing partial work

Whether Opus reviews the existing partial work here is **risk-routed**:

- **STANDARD** — no automatic `arch-reviewer` dispatch. The controller checks
  the Step 3 matrix against the approved plan itself and proceeds. If a genuine
  architectural or product contradiction surfaces, escalate **that specific
  question** to `arch-reviewer` with the established anchors — never the whole
  story.
- **ARCHITECTURE** — dispatch `arch-reviewer` (opus) for a **targeted**
  architecture-only review of the existing work.
- **HIGH_RISK** — the existing full conflict review is retained.

When dispatched, hand **`arch-reviewer`** (opus, read-only) the Step 3 matrix,
the plan path and the concrete anchors — not a fresh investigation — to review
what already exists against the *currently approved* plan and against
`docs/adr/` and `CLAUDE.md`: does the committed code contradict the approved
architecture (wrong boundary, inverted dependency, persistence ownership
elsewhere, a contract the plan does not sanction)? Do not ask it to repeat
repository discovery, implementation planning, acceptance criteria or routine
code review.

`arch-reviewer` recommends and supplies the `DECISION REQUIRED` substance; it
decides nothing. If a contradiction stands: the **controller STOPS** and presents
the `DECISION REQUIRED` block from `/next-story` Phase 6 to the user. If no
unresolved architectural or product decision exists, continue automatically —
do **not** present an empty Decision Gate. Never silently rewrite committed
architecture — reconciling committed code with an approved plan is the user's
decision.

## Step 5 — Linear

If reconciliation is safe and this is not a dry run, move the issue to
**In Progress** if it is not already. Touch no other issue.

## Step 6 — Continue the remainder

Resume from the **first PARTIAL or NOT_STARTED task**. Never regenerate the
intake or the plan, and do not re-investigate COMPLETE plan tasks unless Step 7
verification or the Step 8 review exposes a contradiction.

Dispatch **`story-engineer`** (sonnet) to implement only the PARTIAL and
NOT_STARTED tasks, in plan order, via the Superpowers workflow named in
`/next-story` Phase 7. Hand it the plan path, the current task, the approved
architectural decisions and the list of COMPLETE tasks it must **not**
re-investigate; take back a compact result and dispatch the next task. The plan
was read in full in Step 2 and is not reloaded or restated. Only the checks
relevant to each changed task run here; the full verification is Step 7. Scope
rules from `/next-story` still bind: current story only, no downstream stories,
no silent scope expansion. Implementation-level defects are the engineer's to fix
autonomously; any architectural or product question comes back as an
`ESCALATION REQUIRED` brief, is routed to **`arch-reviewer`** (opus), and
re-enters the same hard Decision Gate (`/next-story` Phase 6) with no exceptions
for work already committed.

## Step 7 — Verify the whole story

The controller enumerates the complete check list and dispatches
**`verify-runner`** (haiku) to run — exactly once — every verification the
approved plan requires, plus the `/next-story` Phase 8 checklist, over the
**entire** story, including work that existed before this invocation.
Pre-existing code is not exempt from verification, and COMPLETE Step 3 tasks are
verified here like everything else. Never claim a command passed without having
run it and read its output. The controller decides whether this gate passes.

Passes come back as `<command> → PASS (<key counts/evidence>)`; failure output is
preserved in full. `verify-runner` never interprets a non-obvious failure — it
returns an `ESCALATION REQUIRED` brief, which the controller routes to
**`story-engineer`** together with the preserved output. After a fix,
re-dispatch the affected checks, and re-dispatch the full suite when the fix
could affect broader behavior or to produce the final completion evidence.

## Step 8 — Review the whole story

Review depth follows the Step 3b classification exactly as in `/next-story`
Phase 9: **STANDARD** — one Sonnet final review; **ARCHITECTURE** — the Step 4
Opus architecture review plus one Sonnet final code review; **HIGH_RISK** — the
existing full review behavior. Never review an unchanged code state twice.

Dispatch **`story-reviewer`** (sonnet, read-only) for one fresh review from
fresh context — not several of the same unchanged code — of the **entire final
story state**, implementation and diff (`git diff main...` plus the working
tree; this is the gate where full diffs are read), including work that predates
this invocation, against: the Linear story, Acceptance Criteria, Business Rules,
the approved plan, ADRs, architecture boundaries, security and scope exclusions.
Implementation-level findings are routed to **`story-engineer`** to fix, then
the affected verification is re-dispatched to `verify-runner`. An architectural
concern is returned for **`arch-reviewer`** (opus) escalation and, if it is a
real architectural/product decision, re-enters `/next-story` Phase 6 — the
reviewer never resolves it. This review is mandatory and independent; the
controller decides whether the gate passes.

## Step 9 — Completion Gate and Publication Gate

The **controller** emits the same 13-point completion report as `/next-story`
Phase 10 directly from workflow state and the agents' compact returns, under its
compact reporting rules (`PASS — <evidence>` for successes; detail only for
failures, deviations, architecture decisions, security concerns, technical debt,
or on request), then **STOP
before any commit, push or PR**. Publication follows `/next-story` Phase 11
unchanged: explicit approval, stage only current-story files, inspect the staged
diff, `feat/crm-<n>-<slug>` branch, no force-push, `gh pr create`, reference the
PR in Linear, move to **In Review** — never Done because a PR exists. The
controller performs the publication itself; no agent may commit, push, open a PR
or write to Linear.

`/next-story`'s **Gates that are never removed** section applies here in full:
risk routing may reduce ceremony, never a gate.

**STOP after one story.** Never start another automatically.
