---
description: Pick and implement the next Ready Squad CRM story from Linear — one story only, optimized for feature delivery speed.
---

Execute one Squad CRM story end to end, directly, on the **Feature Velocity**
workflow.

`$ARGUMENTS` may name a specific issue (e.g. `CRM-106`) or add flags:

- `--dry-run` — run selection and reconciliation only, then stop and report.
  Touch nothing: no Linear write, no file write, no git write.
- empty — auto-select the next eligible story.

## Core rule

Linear is the work tracker, but it is **not** assumed to be synchronized with
Git or `.squad/`. Never begin implementation solely because a Linear issue says
Todo. A lightweight reconciliation check comes before any write of any kind.

## Main-agent execution

**You (the main agent) execute this story directly.** Do not spawn subagents by
default — no mandatory repo-scout, story-engineer, architecture reviewer,
verification runner or story reviewer. Read code, write code, run tests and
talk to Linear yourself.

A fresh subagent reviewer is exceptional, not routine. Dispatch one only when
concrete evidence shows substantial security, data-integrity or shared-
architecture risk — never as a default step, and never merely because a story
"feels" complex.

## Step 1 — Select the story

1. Sync main (`git status`, `git pull` if behind and clean).
2. If the previously active story was merged, close it in Linear (move to
   Done and note the merge) if not already done.
3. Query Linear for eligible candidates in the Squad CRM project. Order by:
   dependency readiness (every `blockedBy` issue Done) → milestone/sprint order
   → priority → position.
4. Select the top eligible candidate. If the top candidate is blocked, report
   the blocked chain and let the user choose rather than silently skipping to
   an unrelated issue.
5. Move exactly the selected issue to **In Progress**. Touch no other issue.

Do not perform broad backlog analysis — this is a metadata-driven pick, not an
audit of the whole backlog.

**Continue immediately after selection — do not stop for approval here.**

Stop here (before any Linear write) and report if invoked with `--dry-run`.

## Step 2 — Lightweight reconciliation

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

## Step 3 — Squad Kit (mandatory, lean)

Every story requires Squad Kit artifacts (intake + implementation plan)
before implementation — this is not optional (see `CLAUDE.md` §4). You (the
main agent) create/reconcile them directly; never spawn a planning subagent.

1. If an approved plan already exists for this story, it is authoritative —
   reuse it as-is. Do not regenerate or rewrite it for formatting.
2. Otherwise create the **minimum complete** intake + plan:
   - straightforward stories (typical CRUD, forms, lists, a normal screen or
     endpoint following established patterns): a concise intake and an
     implementation-focused plan that references existing patterns instead of
     re-documenting them — do not repeat Linear text, reload all ADRs, or
     design downstream stories;
   - complex/security/architecture stories: the plan may go into more detail
     where that materially improves implementation correctness.
3. Treat the plan as the implementation source of truth and continue directly
   into implementation — do not stop after planning unless Step 4 finds a
   genuine Decision Gate.
4. At completion, verify the implementation against both the Linear AC/BR and
   the Squad Kit plan (Step 7).

## Step 4 — Decision Gate (only for genuine decisions)

Stop and ask the user only for a genuine:

- product/observable-behavior ambiguity that changes the feature;
- architecture boundary or shared-contract decision;
- security/data-integrity decision;
- source-of-truth conflict (Linear vs. `.squad/` vs. code vs. ADR).

Ordinary implementation decisions (naming, package patch versions, test
fixture mechanics, anything inside an already-established pattern) are
autonomous — do not turn them into Decision Gates.

When a genuine decision is needed, present it concisely (decision, options,
tradeoffs, recommendation) and wait. Implement nothing until resolved.

## Step 5 — Implementation (YAGNI)

Implement the smallest complete solution satisfying the current story only.

Do not: build downstream stories; create speculative abstractions; add
provider/framework layers without a current consumer; generalize for
hypothetical future requirements; refactor unrelated code.

Reuse established project patterns aggressively. Use a
`feat/crm-<n>-<slug>` branch. Follow relevant installed Superpowers skills
(e.g. `test-driven-development`, `systematic-debugging`) as they naturally
apply — do not layer a separate methodology on top.

## Step 6 — Verification (proportional to risk)

For a **STANDARD** feature story:

- affected backend/frontend tests;
- build/type-check/lint as applicable;
- migration check when persistence changes;
- browser smoke when observable UI changed (see below);
- final diff review.

Do not run the entire repository test suite merely as ceremony — CI is
responsible for repository-wide regression gates.

Automatically **expand** verification to the relevant integration/
architecture/security suites when the story touches authentication,
authorization, security, migrations, shared architecture or data integrity.
Never reduce verification required by explicit Acceptance Criteria.

**Browser verification (UI stories):** verify only representative behavior —
feature works; desktop/mobile does not visibly break; Arabic/English and
RTL/LTR when relevant; important interactions work. Do not exhaustively
retest the whole application per story.

## Step 7 — Completion report

Keep it concise:

```
STORY COMPLETE — CRM-XXX

Implemented
Verification
AC/BR
Risks/deviations
Git status

PUBLICATION APPROVAL REQUIRED
```

Do not produce long file inventories unless requested or materially useful.

## Step 8 — Publication safety (never automatic)

Never commit, push, open a PR, merge, or make completion-related Linear
changes without explicit user approval.

After approval:

1. inspect the final scope (`git status`, `git diff --cached` once staged);
2. stage only current-story files;
3. commit on the `feat/crm-<n>-<slug>` branch — never directly to `main`;
4. push the branch (never force-push unless explicitly approved);
5. open the PR with `gh pr create`;
6. link the PR on the Linear issue and move it to **In Review**;
7. wait for required CI;
8. **STOP before merge.**

Never automatically start another story.

## Safety rules (never weakened for speed)

Do not weaken: architecture boundaries; authorization/security; data
integrity; module ownership; Acceptance Criteria; Business Rules; CI gates;
publication approval. Speed comes from removing redundant process
(mandatory subagents, ceremony plans, blanket reviews), never from skipping
correctness.
