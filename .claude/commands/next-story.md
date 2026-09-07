---
description: Pick and implement the next Ready Squad CRM story from Linear — one story only, Fast Lane delivery to an open PR.
---

Execute one Squad CRM story end to end, directly, on the **Fast Lane**
workflow: selection → Squad Kit → implementation → proportional verification
→ commit/push/PR/CI, with no intermediate user approvals.

`$ARGUMENTS` may name a specific issue (e.g. `CRM-106`) or add flags:

- `--dry-run` — run selection and reconciliation only, then stop and report.
  Touch nothing: no Linear write, no file write, no git write.
- empty — auto-select the next eligible story.

## Core rule

Linear is the work tracker, but it is **not** assumed to be synchronized with
Git or `.squad/`. Never begin implementation solely because a Linear issue says
Todo. A lightweight reconciliation check comes before any write of any kind.

## Main-agent execution

**You (the main agent) execute this story directly.** Zero subagents by
default — no repo-scout, story-engineer, architecture reviewer, verification
runner or story reviewer. Read code, write code, run tests and talk to Linear
yourself.

A subagent is exceptional. Dispatch one only when concrete evidence shows
substantial security, data-integrity or shared-architecture risk — never as a
default step, and never merely because a story "feels" complex.

## Step 1 — Sync and reconcile the previous story

1. Sync main (`git status`, `git pull` if behind and clean).
2. Reconcile **only** the previously merged story: if its PR is confirmed
   merged with required CI green, move it to Done in Linear (noting the merge)
   if it is not already Done.
3. Do not perform broad backlog reconciliation — no audit of other issues.

## Step 2 — Select exactly one story

1. Query Linear for eligible candidates in the Squad CRM project. Order by:
   dependency readiness (every `blockedBy` issue Done) → milestone/sprint order
   → priority → position. Fall back to Backlog when no Ready candidate exists.
2. Select the top eligible candidate. If the top candidate is blocked, report
   the blocked chain and let the user choose rather than silently skipping to
   an unrelated issue.
3. Move exactly the selected issue to **In Progress**. Touch no other issue.
   **Never auto-start more than one story.**

This is a metadata-driven pick, not a backlog audit.

**Continue immediately after selection — do not stop for approval here.**

Stop here (before any Linear write) and report if invoked with `--dry-run`.

## Step 3 — Lightweight reconciliation

Before writing any code, check only:

- the active Linear story's description, Acceptance Criteria, Business Rules;
- its direct blockers' status;
- `git log --oneline -20`, `git branch -a --list 'feat/crm-<n>-*'`,
  `git status --porcelain`, and whether a matching `.squad/plans/` file exists;
- the relevant existing implementation the story will touch.

Do not repeatedly load unrelated stories, all ADRs, broad git history, or the
full repository structure — that context is already established or is not
needed for this story.

If this evidence shows the story is already implemented, partially
implemented, or in conflicting state, **stop** and report what you found
instead of overwriting or re-implementing it — recommend `/resume-story`
instead.

## Step 4 — HARD SQUAD KIT GATE (mandatory, lean)

Before modifying **any** application code, the active story MUST have:

- a valid Squad Kit intake, and
- a valid Squad Kit implementation plan.

This gate is mandatory for every story and is never weakened for speed. You
(the main agent) create/reconcile the artifacts directly; never spawn a
planning subagent.

1. Reuse existing valid artifacts as-is. Do not regenerate or rewrite a valid
   plan for formatting.
2. If either artifact is missing or invalid, create it **now**, before
   implementation:
   - straightforward stories (typical CRUD, forms, lists, a normal screen or
     endpoint following established patterns): a concise intake and an
     implementation-focused plan that references existing patterns instead of
     re-documenting them;
   - complex/security/architecture stories: more detail where that materially
     improves implementation correctness.
3. Do not duplicate Linear requirements or unrelated ADR content in the
   artifacts.
4. **No separate plan approval is required.** Treat the plan as the
   implementation source of truth and continue directly into implementation.

## Step 5 — Decision Gate (only for genuine decisions)

Stop and ask the user only for a genuine:

- product/observable-behavior ambiguity that changes the feature;
- architecture boundary or shared-contract decision;
- security/data-integrity decision;
- source-of-truth conflict (Linear vs. `.squad/` vs. code vs. ADR).

Ordinary implementation decisions (naming, package patch versions, test
fixture mechanics, anything inside an already-established pattern) are
autonomous and must not stop execution.

When a genuine decision is needed, present it concisely (decision, options,
tradeoffs, recommendation) and wait. Implement nothing until resolved.

## Step 6 — Implementation (YAGNI)

Implement the smallest complete solution satisfying the current story only,
directly in the main agent.

Do not: build downstream stories; create speculative abstractions; add
provider/framework layers without a current consumer; generalize for
hypothetical future requirements; refactor unrelated code.

Reuse established repository patterns aggressively. Use a
`feat/crm-<n>-<slug>` branch. Do not perform broad repository discovery when
targeted reads suffice, and do not reread unrelated ADRs or plans. Follow
relevant installed Superpowers skills as they naturally apply — do not layer a
separate methodology on top.

## Step 7 — Verification (proportional to risk)

**STANDARD story:**

- affected backend/frontend tests;
- relevant build/type-check/lint;
- required formatting gates (below);
- migration verification when persistence changed;
- focused browser smoke when new/changed UI behavior materially benefits from
  runtime verification.

**HIGH-RISK / CROSS-CUTTING story** (auth/security, migrations/data integrity,
shared architecture or contracts, module boundaries): expand to the relevant
integration/architecture/security suites. Never reduce verification required
by explicit Acceptance Criteria.

Do not run the entire repository test suite locally for every ordinary story
merely for ceremony — **CI owns broad regression coverage.**

Required formatting gates (always):

```
npm run format:check --prefix src/frontend
dotnet format src/backend/SquadCrm.sln --no-restore --verify-no-changes
```

**Browser verification (UI stories):** representative behavior only — feature
works; desktop/mobile does not visibly break; Arabic/English and RTL/LTR when
relevant; important interactions work. Do not exhaustively retest the whole
application per story.

## Step 8 — Pre-publication check

Before publishing:

1. compare the implementation against the Linear AC/BR and the Squad Kit plan;
2. inspect the final diff;
3. ensure no unrelated changes are included;
4. update the Squad plan only for meaningful implementation deviations.

## Step 9 — Fast-Lane publication (no approval gate)

If implementation and required local verification are green, continue
automatically — **do not** ask for a separate publication approval:

1. stage only story-related files;
2. inspect the staged diff (`git diff --cached`);
3. commit on the `feat/crm-<n>-<slug>` branch — never directly to `main`;
4. push the branch (never force-push unless explicitly approved);
5. open or update the PR with `gh pr create` / `gh pr edit`;
6. link the PR on the Linear issue and move it to **In Review** as
   appropriate;
7. let required CI run and check its result.

**CI failure:** diagnose and fix failures that belong to the active story,
push the fix, and re-check CI. Do not ask for approval for ordinary
story-owned fixes.

**Unrelated pre-existing defect discovered:** do not silently fix it. Create
or link a separate Linear bug when appropriate, continue the active story if
the defect does not prevent its AC/BR, and stop only if it genuinely blocks
correctness.

## Step 10 — Merge approval (hard stop)

When required CI is green, stop with exactly:

```
MERGE APPROVAL REQUIRED
Story: CRM-xxx
PR: #xx
Local verification: PASS
CI: PASS
Outstanding: none | concise list
```

- **NEVER** merge the PR automatically.
- **NEVER** start the next story automatically.

## Completion reports

Keep routine reports concise. Do not narrate every file and test unless
something exceptional happened.

```
CRM-128 READY TO MERGE
PR #31
Local: PASS
CI: PASS
Deviations: none

MERGE APPROVAL REQUIRED
```

## Token / speed rules

- zero subagents by default;
- targeted reads over broad scans;
- no duplicate reconciliation;
- no repeated full-plan or ADR reads;
- no repeated tests for untouched code;
- no verbose progress reports;
- Linear calls only when needed;
- preserve valid evidence across resumed sessions;
- CI handles broad regression for standard stories.

## Safety rules (never weakened for speed)

Do not weaken: the Squad Kit gate; architecture boundaries;
authorization/security; data integrity; module ownership; Acceptance
Criteria; Business Rules; CI gates; the merge-approval stop. Speed comes from
removing redundant process (mandatory subagents, ceremony plans, blanket
reviews, intermediate approvals), never from skipping correctness.
