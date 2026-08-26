---
description: Pick, reconcile, plan, implement and publish the next Ready Squad CRM story from Linear — one story only, with mandatory stop gates.
---

Orchestrate one Squad CRM story end to end:

Linear MCP → repository reconciliation → Squad Kit intake → Squad Kit plan →
plan review → Architecture Decision Gate → Superpowers implementation →
verification → independent review → completion report → Git/PR → Linear.

`$ARGUMENTS` may name a specific issue (e.g. `CRM-106`) or add flags:

- `--dry-run` — run Phase 1 and Phase 2 only, then stop and report. Touch
  nothing: no Linear write, no file write, no git write.
- empty — auto-select the next eligible story.

## Core rule

Linear is the work tracker, but it is **not** assumed to be synchronized with
Git or `.squad/`. Never begin implementation solely because a Linear issue says
Todo. Reconciliation (Phase 2) is mandatory and comes before any write of any
kind.

## Token discipline (never at the cost of a gate)

Efficiency is about *how much you read and print*, never about which gate you
run. The Reconciliation Gate, Architecture/Product Decision Gate, Acceptance
Criteria verification, architecture/security checks, final full verification,
independent final review and Publication Gate are all mandatory and are never
skipped, sampled or shortened.

Within that constraint:

- **Progressive depth.** Start with cheap signals; escalate to deep inspection
  only where the cheap signals point at prior or conflicting work.
- **Targeted reads.** Prefer `grep`/`git log --grep`/`ls`/`test -f` and ranged
  reads over dumping whole files, plans, diffs or logs.
- **Read once, reuse.** Read the approved plan completely once when you start
  the story; afterwards work from the current task/section. Do not reload or
  restate the whole plan unless new evidence contradicts it.
- **Fetch Linear once per phase** where freshness matters (Phase 1/2 discovery,
  and again immediately before a tracker write). Do not re-fetch unchanged
  metadata.
- **Compact successful output.** Report a passing command as
  `<command> → PASS (<key counts/evidence>)`. Keep failure output in full while
  debugging.
- **No duplicate work.** One review per unchanged code state; no parallel agents
  or extra skills that redo an investigation already done. Use a subagent only
  when isolation or genuinely parallel investigation adds value.
- **Working summary.** Maintain a compact running state: current story,
  approved architectural decisions, current task, completed task statuses,
  unresolved findings, verification state. Do not restate conversation history
  or finished-task detail.

## Project facts (verify, do not assume)

- Linear project **Squad CRM**, team **Mahmoud Said**. Statuses in this team:
  Backlog, Todo, In Progress, In Review, Done, Canceled, Duplicate.
- Squad Kit: `squad` CLI on PATH, workspace in `.squad/`, config
  `.squad/config.yaml`. Intakes in `.squad/stories/`, plans in `.squad/plans/`.
  Note `tracker.type: none` while plans carry `CRM-nnn` ids — surface that
  mismatch if it obstructs you; do not silently rewrite the config.
- Superpowers plugin enabled (`.claude/settings.json`).
- Architecture sources of truth: `CLAUDE.md`, `docs/adr/ADR-0*.md`.
- Git: `origin` = `github.com/MahmoudSaidX/crm`, default branch `main`,
  branch convention `feat/crm-<n>-<slug>`, PRs via `gh`.

---

## Phase 1 — Discover

Use Linear MCP (`list_issues`, `get_issue` with `includeRelations: true`) on the
Squad CRM project. Order candidates by:

1. dependency readiness — **every** `blockedBy` issue is Done;
2. Sprint/milestone order (`projectMilestone`, lowest sprint first);
3. explicit Linear priority, then position.

A story is eligible only when all blocking issues are Done. If the top
candidate's dependencies are unresolved, do not silently skip to an unrelated
issue — report the blocked chain and let the user choose.

Rank from Linear metadata and relations alone — this is a metadata pass. Do
**not** inspect implementation for every candidate; deep inspection happens in
Phase 2 for the selected story only.

Report the ranked shortlist and the one you selected, with its blockers and
their statuses.

## Phase 2 — Reconciliation Gate (mandatory, read-only)

Before writing to Linear or the repository, gather evidence **progressively**.
The gate itself is never skipped — only the depth of inspection adapts.

**Pass 1 — cheap signals (always, for the selected story only):**

- **Linear state** — status, statusType, stateHistory, attachments (PR links),
  from the single fetch already made in Phase 1.
- **Git history** — `git log --oneline -20`, `git log --all --grep=<ID> -i`,
  `git branch -a --list 'feat/crm-<n>-*'`,
  `gh pr list --state all --search <ID>`.
- **Current branch** — `git rev-parse --abbrev-ref HEAD`; if candidate commits
  exist, `git branch --contains <sha>`.
- **Working tree** — `git status --porcelain`.
- **Artifact existence** — does a matching `.squad/stories/**/intake.md` and
  `.squad/plans/**/NN-story-*<slug>*.md` exist?

**Pass 2 — deep implementation inspection (required whenever Pass 1 shows any
repository evidence of prior or conflicting work:** a matching commit, branch,
PR, related working-tree change, or an existing plan; also whenever Pass 1 is
ambiguous):

- Read the approved plan once, in full, then walk its task list and check each
  named file/symbol actually exists — `git log -1 --stat <sha>`, targeted
  `grep`/`test -f` per named artifact rather than whole-file dumps. Absence of a
  `DbContext`, a migration, a test project or a doc update is evidence of
  partial work, not completion.

If Pass 1 shows no plan, no intake, no commit, no branch, no PR and a clean
tree, that is itself sufficient evidence for class A — say so explicitly rather
than enumerating a per-task matrix of a plan that does not exist.

Then classify as exactly one of:

| Class | Meaning |
|---|---|
| **A. NOT_STARTED** | No plan, no intake, no code, no branch, no PR |
| **B. PLANNED_NOT_IMPLEMENTED** | Intake and/or plan exist; no implementation code |
| **C. PARTIALLY_IMPLEMENTED** | Some plan tasks implemented, others missing |
| **D. IMPLEMENTED_NOT_REVIEWED** | Implementation looks complete but unverified/unreviewed |
| **E. COMPLETED** | Verified, merged/complete implementation — tracker mismatch |
| **F. CONFLICTED** | Evidence is contradictory or ambiguous |

**Only A and B may continue.** For **C, D, E or F: STOP immediately** and report:

1. Linear state (status + history);
2. Git evidence (commits, shas, branch, PR, containment, working tree);
3. Squad Kit evidence (intake present/valid, plan present/approved);
4. Implementation evidence (plan task → present/absent, per file);
5. Recommended tracker action and recommended repository action.

Never overwrite, regenerate or reimplement existing work to force a clean
start. Continuing partial work is a decision for the user, not for you.

Stop here and report if invoked with `--dry-run`.

## Phase 3 — Linear start

Only after Phase 2 returns A or B: move the selected issue to **In Progress**
via `save_issue`. Modify no other issue. Record the issue ID and title for
every later phase.

## Phase 4 — Squad Kit intake

If no valid intake exists, create it with the repository's installed workflow —
`/squad-new-story` (which runs `squad new-story`). Do not hand-roll an intake
format.

Populate it from Linear MCP data: story title, full description, Acceptance
Criteria, Business Rules, Fields Dictionary (when present), `blockedBy` and
`blocks` relations with their statuses, and relevant metadata (milestone,
estimate, labels, priority).

Then enrich **only** from: repository architecture, `docs/adr/`, established
project decisions in `CLAUDE.md`, and the outputs of already-completed
prerequisite stories.

- Do not invent product requirements. Missing requirements are a question for
  the user, not a gap to fill.
- Do not rely on Linear URLs inside the intake — copy the content in, because
  the Squad Kit planner reads only the intake and its `attachments/`.
- If a valid intake already exists, reconcile it against Linear and report any
  drift. Never silently overwrite an approved decision.

## Phase 5 — Squad Kit planning

Generate the plan with the installed planner: `/squad-plan <intake-path>`.
Do not substitute a custom planning system, and do not regenerate an existing
approved plan without the user's explicit go-ahead.

Read the generated plan completely, once. Then review it against: the story, Acceptance Criteria,
Business Rules, dependencies, `docs/adr/`, current architecture, prior
foundation-story decisions, this story's scope, and downstream-story
boundaries. Report review findings before proceeding.

## Phase 6 — Architecture Decision Gate

Classify every unresolved item from the plan review as exactly one of:

**IMPLEMENTATION_CHOICE** — resolve independently. E.g. a concrete compatible
package patch version, method/class naming, test-fixture mechanics, any detail
inside an already-approved boundary.

**ARCHITECTURAL_PRODUCT_DECISION** — **STOP.** E.g. a new project or module
boundary; changing dependency direction; database/persistence ownership; API
contract or versioning; authentication/security architecture;
transaction/event boundaries; a technology or provider change; anything
contradicting an ADR; scope expansion; responsibility moving between stories.

When stopping, output exactly:

```
DECISION REQUIRED

Story:
Decision:
Context:

Option A:
Pros:
Cons:

Option B:
Pros:
Cons:

Recommendation:
Reason:

Impact on current plan:
```

Implement nothing until explicitly approved. After approval, reconcile the plan
through the Squad Kit workflow before implementing.

## Phase 7 — Implementation

The **approved** Squad Kit plan is the source of truth; you have already read
it in full, so work from the current task/section and re-read other parts only
when evidence forces a re-evaluation. Invoke the relevant
installed Superpowers skills rather than a duplicate methodology — typically
`superpowers:using-git-worktrees` or a `feat/crm-<n>-<slug>` branch,
`superpowers:test-driven-development`, `superpowers:executing-plans`, and
`superpowers:systematic-debugging` for any failure.

Implement only the current story. Never implement a downstream story early —
if the plan's non-goals name it, it stays unimplemented. Implementation-level
failures are yours to fix autonomously; a newly surfaced architectural or
product question sends you back to Phase 6.

While implementing, run only the tests/checks relevant to the task you just
changed — the complete plan-required verification runs once at Phase 8.

## Phase 8 — Verification (Completion Gate)

Once the implementation is stable, run the **complete** required verification
exactly once — every verification the approved plan specifies, plus, as
applicable:
build; lint/format; unit tests; integration tests; architecture tests;
migration/infrastructure checks; dependency-boundary checks; each Acceptance
Criterion; each Business Rule; scope exclusions; no secrets committed; no
debug/temp code left; `git diff` and `git status`. Use targeted git commands
during implementation; read the full diff here and at the Publication Gate,
where it matters.

Follow `superpowers:verification-before-completion`: **never state that a
command passed unless you actually ran it and read its output.** Record a pass
compactly as `<command> → PASS (<key counts/evidence>)`; keep failure output in
full while debugging.

An implementation defect: fix, then rerun the affected checks. Rerun the full
suite when the fix could affect broader behavior, and always for the final
completion evidence — the story is never reported complete on partial reruns.
A fix that would require changing approved architecture: stop at Phase 6.

## Phase 9 — Independent review

Review the finished work fresh (`superpowers:requesting-code-review`) against:
(1) the Linear story, (2) Acceptance Criteria, (3) Business Rules, (4) the
approved plan, (5) ADRs, (6) architecture boundaries, (7) scope exclusions,
(8) security/secrets, (9) dependency direction, (10) tests and verification
evidence. Fix implementation-level findings autonomously and rerun the affected
verification. Apply `superpowers:receiving-code-review` — verify a finding
before acting on it.

This independent review is mandatory and happens once over the final code
state; do not run duplicate reviews of the same unchanged code.

## Phase 10 — Completion report

Before any Git publication, report — concisely. Successful items are one line:
`PASS — <evidence>`. Expand into detail only for failures, deviations,
architecture decisions, security concerns, technical debt, or detail the user
asked for. Never repeat information already stated elsewhere in the report.

1. story implemented; 2. implementation summary; 3. architecture/dependency
changes; 4. files created/modified/deleted; 5. packages added;
6. verification commands with exact results; 7. each Acceptance Criterion with
its evidence; 8. Business Rules verification; 9. deviations from the approved
plan; 10. warnings/technical debt; 11. scope-creep check;
12. `git status --short`; 13. proposed commit message.

## Phase 11 — Publication Gate

**Do not publish automatically.** STOP before commit/push/PR and request
approval, showing the completion report and proposed commit message.

After explicit approval:

- stage **only** current-story files; exclude pre-existing and unrelated
  working-tree changes and any generated/runtime artifact (`obj/`, `bin/`,
  build output);
- inspect the staged diff (`git diff --cached`) before committing;
- commit on a `feat/crm-<n>-<slug>` branch — never commit directly to `main`;
- push the feature branch; never force-push unless explicitly approved;
- open the PR with `gh pr create`.

Then in Linear: attach or reference the PR on the issue and move the issue to
**In Review**. Do **not** set Done merely because a PR exists — Done follows
the team's actual merge/completion workflow.

**STOP after one story.** Never start the next one automatically.

---

## Safety rules

Never: trust Linear state without repository reconciliation; overwrite a
partial implementation; regenerate an approved plan unnecessarily; change
architecture silently; expand scope silently; rewrite an ADR unless the
approved plan explicitly requires it; include unrelated working-tree changes;
commit generated or runtime artifacts; force-push; mark an issue Done because
implementation exists; continue automatically to another story.

## Reconciliation regression case — CRM-106

A dry run must reproduce this. Linear says CRM-106 is **Todo**; commit
`35b49f9` on `main` carries partial CRM-106 work (Postgres config adapter,
`PersistenceProbe` scaffolding, EF tool manifest) while the approved plan's
Tasks 4–8 and 10 are absent (no `ArchitectureFixtureDbContext`, no design-time
factory, no `AddDbContext` registration, no migration, no
`PersistenceArchitectureRulesTests`, no `SquadCrm.Persistence.IntegrationTests`,
EF Core/Npgsql still in `ForbiddenAssemblyPrefixes`, README placeholder
unresolved).

Expected classification: **C. PARTIALLY_IMPLEMENTED**.
Expected behavior: **STOP**, report the evidence, and recommend resuming
CRM-106 from the approved plan's Task 4 — never regenerating the plan or
reimplementing what commit `35b49f9` already landed.
