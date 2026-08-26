---
description: Pick, reconcile, plan, implement and publish the next Ready Squad CRM story from Linear — one story only, with mandatory stop gates.
---

Orchestrate one Squad CRM story end to end, on the **risk-routed Workflow V2**
path:

Linear MCP → repository reconciliation → **risk classification** → Squad Kit
intake → Squad Kit plan → (architecture review only when the risk level calls
for it) → Decision Gate (only when a real decision exists) → Superpowers
implementation → verification → independent review → completion report →
Git/PR → Linear.

Ceremony scales with risk. Safety gates do not: the gates listed under
**Gates that are never removed** run at every risk level.

`$ARGUMENTS` may name a specific issue (e.g. `CRM-106`) or add flags:

- `--dry-run` — run Phase 1 and Phase 2 only, then stop and report. Touch
  nothing: no Linear write, no file write, no git write.
- empty — auto-select the next eligible story.

## Core rule

Linear is the work tracker, but it is **not** assumed to be synchronized with
Git or `.squad/`. Never begin implementation solely because a Linear issue says
Todo. Reconciliation (Phase 2) is mandatory and comes before any write of any
kind.

## Model routing (do not use slash-command `model:`)

Slash-command `model:` frontmatter is ignored — it inherits the session model.
Model selection happens **only** through subagent routing, which is verified to
work for Haiku, Sonnet and Opus with no observed restriction or fallback.

**This interactive main thread is the CONTROLLER.** It sequences phases, owns
every human stop point, preserves workflow state, receives compact agent
results, classifies reconciliation results, presents `DECISION REQUIRED`,
requests publication approval, and dispatches the next phase.

The controller does **not** itself perform large repository reads,
implementation, planning, debugging, or repetitive verification when a routed
agent owns that work. It does perform small mechanical operations directly
(Linear status writes, the completion report, staging/commit/push/PR after
approval) — there is no reporter agent and no linear-publisher agent.

| Agent | Model | Effort | Owns | Mode |
|---|---|---|---|---|
| `repo-scout` | haiku | low | Linear discovery, dependency/status inspection, git history/status/branch evidence, Squad Kit artifact discovery, reconciliation evidence, resume task-matrix evidence | read-only |
| `story-engineer` | sonnet | medium | intake prep needing interpretation, Squad Kit plan generation, implementation, refactoring, debugging, task-relevant incremental verification | writes code |
| `arch-reviewer` | opus | high | approved-plan architecture review, ADR consistency, module/dependency boundaries, persistence/transaction/integration, security architecture, Decision Gate analysis | read-only |
| `verify-runner` | haiku | low | the final plan-required build/test/lint/migration verification, concise PASS evidence, preserved failure output | no fixes |
| `story-reviewer` | sonnet | medium | independent final review of the whole final story state from fresh context | read-only |

### Authority is never delegated

Agents supply **evidence and recommendations**. The **controller** decides
whether a gate passes. The **user** decides every architectural/product
decision and every publication. An agent may never classify a reconciliation
class, declare a gate passed, approve a plan, or publish.

### Escalation

Every agent returns, instead of guessing:

```
ESCALATION REQUIRED
Current phase:
Question:
Evidence:
- file:line / command / exact error
Already established:
Why higher-level reasoning is required:
Recommended next model:
- Sonnet | Opus
```

**Haiku → Sonnet** (`repo-scout`/`verify-runner` → `story-engineer`) when code
meaning must be reasoned about, a failure cause is non-obvious, multiple
behaviors interact, or findings conflict.

**Sonnet → Opus** (`story-engineer`/`story-reviewer` → `arch-reviewer`) when
architecture boundaries may change, approved-plan assumptions are challenged,
multiple architectural options exist, or an ADR / security / product /
persistence / transaction / integration decision is required.

On escalation the controller passes **the compact brief plus the relevant
anchors only**. Investigation already done is never redone. Delegate only when
the read-set is substantially larger than the return-set; keep a one-file check
or a single git command in the controller.

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
- **Proportional plans.** Plan size follows story complexity, not habit. For a
  STANDARD story prefer a concise implementation plan with exactly: scope;
  dependencies; relevant existing patterns; implementation steps;
  acceptance-criteria verification; out of scope. Do not expand a routine story
  into an architecture document, and do not restate the Linear description
  verbatim more than once.
- **Compact returns.** Agents return summaries, not full file dumps, full
  command output or repeated evidence, when a `path:line` anchor or a concise
  result is sufficient. Keep full output only for failures or when a decision
  depends on it.
- **Fetch Linear once per phase** where freshness matters (Phase 1/2 discovery,
  and again immediately before a tracker write). Do not re-fetch unchanged
  metadata.
- **Compact successful output.** Report a passing command as
  `<command> → PASS (<key counts/evidence>)`. Keep failure output in full while
  debugging.
- **No duplicate work.** One review per unchanged code state; no parallel agents
  or extra skills that redo an investigation already done. Route work to the
  agent that owns it (see **Model routing**), and delegate only when the
  read-set is substantially larger than the compact return-set.
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

Dispatch **`repo-scout`** (haiku) to collect the discovery evidence read-only:
Linear MCP (`list_issues`, `get_issue` with `includeRelations: true`) on the
Squad CRM project, returning per candidate the status/statusType, milestone,
priority, position and every `blockedBy`/`blocks` relation with its status. The
controller does the ranking and the selection from that compact evidence — the
scout never selects a story.

Order candidates by:

1. dependency readiness — **every** `blockedBy` issue is Done;
2. Sprint/milestone order (`projectMilestone`, lowest sprint first);
3. explicit Linear priority, then position.

A story is eligible only when all blocking issues are Done. If the top
candidate's dependencies are unresolved, do not silently skip to an unrelated
issue — report the blocked chain and let the user choose.

Rank from Linear metadata and relations alone — this is a metadata pass. Do
**not** inspect implementation for every candidate; deep inspection happens in
Phase 2 for the selected story only.

If the scout escalates (ambiguous or conflicting tracker evidence), the
controller resolves the question or presents it to the user — it does not lower
the eligibility bar.

Report the ranked shortlist and the one you selected, with its blockers and
their statuses.

## Phase 2 — Reconciliation Gate (mandatory, read-only)

Before writing to Linear or the repository, gather evidence **progressively**.
The gate itself is never skipped — only the depth of inspection adapts.

**Routing.** Dispatch **`repo-scout`** (haiku, read-only) to gather both passes
below and return compact structured evidence with an anchor per item. The
controller does **not** run the sweep itself and does not read the repository
broadly here. The controller then performs the classification — the scout
reports presence/absence only and never names a class. Escalate a scout finding
to **`story-engineer`** (sonnet) when the meaning of existing code must be
reasoned about, and from there to **`arch-reviewer`** (opus) when committed code
may contradict approved architecture.

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

Then the **controller** classifies — from the scout's evidence, on its own
judgment — as exactly one of:

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

## Phase 2b — Story Risk Classification (controller, mandatory)

After reconciliation, and before any intake or plan work, the **controller**
classifies the selected story into exactly one level. This classification
selects the workflow route for every later phase. It is never delegated to an
agent.

**STANDARD** — typical business implementation: CRUD; forms; tables/lists;
normal Angular screens; normal module endpoints; straightforward business rules;
implementation that follows an already-established project pattern.

**ARCHITECTURE** — cross-cutting or structural work: shared infrastructure; new
module boundaries; provider/integration foundations; persistence architecture
changes; shared API conventions; observability/platform foundations; changes to
boundaries or contracts shared across multiple modules. A normal business
workflow touching multiple modules is **not** automatically ARCHITECTURE if it
follows already-established contracts and patterns.

**HIGH_RISK** — security- or correctness-sensitive work: authentication;
authorization/permissions; secrets; security boundaries; transaction
boundaries; financial or data-integrity-sensitive behavior; sensitive external
integrations.

Choose the higher risk level **only** when there is concrete evidence that the
story changes a shared architectural boundary/contract, introduces a security or
data-integrity concern, or materially affects an established cross-cutting
convention. Mere uncertainty or implementation complexity alone is not
sufficient to escalate.

Print exactly, and nothing else, for this phase:

```
RISK: STANDARD | ARCHITECTURE | HIGH_RISK
Reason: <one concise sentence>
```

### Route selected by the classification

| Level | Route |
|---|---|
| **STANDARD** | `repo-scout` → `story-engineer` (plan + implement) → `verify-runner` → `story-reviewer` → Publication Gate. **No automatic `arch-reviewer`.** No separate architecture review before implementation. |
| **ARCHITECTURE** | `repo-scout` → `story-engineer` planning → `arch-reviewer` (opus) **targeted plan review** → Decision Gate *only if* unresolved architectural/product decisions exist → `story-engineer` implementation → `verify-runner` → `story-reviewer` → Publication Gate. |
| **HIGH_RISK** | The full workflow and every gate, unchanged: `repo-scout` → `story-engineer` planning → `arch-reviewer` full review → Decision Gate → implementation → `verify-runner` → `story-reviewer` (with the existing full review behavior) → Publication Gate. Use Opus wherever architecture or security judgment is materially useful. |

At **no** level may security, authorization, transaction, secret-management or
data-integrity verification be weakened to save tokens or time.

**STANDARD escalation, not blanket review.** If a genuine architectural or
product decision surfaces during STANDARD planning or implementation, stop that
path and escalate **the specific question** to `arch-reviewer` (opus) with the
established anchors. Never ask Opus to review the whole story when one decision
needs review.

## Phase 2c — YAGNI Gate (all risk levels)

This gate binds planning, architecture review, implementation and review alike.

Do not add an abstraction, extension point, framework, generic infrastructure,
configuration mechanism, provider interface, registry or future-proofing
structure solely because a future story might need it.

New structural complexity must have at least one of:

1. a requirement in the current story;
2. a current consumer;
3. an existing ADR requiring it;
4. evidence that changing it later would cause a significant breaking change.

Otherwise **defer it**.

Prefer extending an established project pattern over creating a new pattern. Do
not solve requirements owned by downstream stories. The controller rejects a
plan, an implementation or a review recommendation that violates this gate.

## Phase 3 — Linear start

Only after Phase 2 returns A or B: move the selected issue to **In Progress**
via `save_issue`. Modify no other issue. Record the issue ID and title for
every later phase.

## Phase 4 — Squad Kit intake

If no valid intake exists, create it with the repository's installed workflow —
`/squad-new-story` (which runs `squad new-story`). Do not hand-roll an intake
format.

**Routing.** Mechanical scaffolding and a straight copy of Linear content stay
with the controller. As soon as the intake needs **interpretation** — enriching
from repository architecture, reconciling an existing intake against Linear,
mapping prerequisite-story outputs — dispatch **`story-engineer`** (sonnet) with
the Linear content and the intake path, and take back a compact result.

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

Dispatch **`story-engineer`** (sonnet) to generate the plan with the installed
planner: `/squad-plan <intake-path>`. Do not substitute a custom planning
system, and do not regenerate an existing approved plan without the user's
explicit go-ahead.

Keep the plan proportional to the story (see **Proportional plans**) and apply
the **Phase 2c YAGNI Gate** to it.

Architecture review of the plan is **risk-routed**:

- **STANDARD** — no `arch-reviewer` dispatch, and no separate architecture
  review before implementation. Proceed straight to Phase 7. Escalate only a
  specific architectural/product question if one genuinely surfaces.
- **ARCHITECTURE** — dispatch **`arch-reviewer`** (opus, read-only) for a
  **targeted** plan review: architecture only. It reviews module/dependency
  boundaries, persistence/transaction/integration decisions, ADR consistency,
  shared-conventions impact and security architecture. Do **not** ask it to
  repeat repository discovery, redo implementation planning, restate acceptance
  criteria, or perform routine code review.
- **HIGH_RISK** — dispatch **`arch-reviewer`** (opus, read-only) for the full
  plan review against: the story, Acceptance Criteria, Business Rules,
  dependencies, `docs/adr/`, current architecture, prior foundation-story
  decisions, this story's scope, and downstream-story boundaries.

In all cases the reviewer recommends; it approves nothing. The controller does
not read the whole plan itself here — it keeps the plan path, the task list and
the returned findings as workflow state, and reports the findings before
proceeding.

## Phase 6 — Architecture Decision Gate (only when a real decision exists)

This gate is entered when there is an unresolved item to decide. If the
architecture review found **no** unresolved architectural or product decision,
continue automatically to Phase 7 — do **not** present an empty Decision Gate.
For a STANDARD story with no escalation, this phase does not run at all; it
still opens on demand for any escalated question, at any risk level.

**Routing.** The `arch-reviewer` (opus) analysis from Phase 5 — or the single
escalated question from a STANDARD run, or a further dispatch if new items
appeared — supplies the substance. The
**controller** classifies each item and owns the gate; the **user** owns the
decision. `IMPLEMENTATION_CHOICE` items are handed to `story-engineer`;
`ARCHITECTURAL_PRODUCT_DECISION` items stop the workflow here.

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

Implement nothing until explicitly approved. The controller presents this block
and waits for the user; it never resolves an `ARCHITECTURAL_PRODUCT_DECISION`
itself and never lets an agent resolve one. After approval, reconcile the plan
through the Squad Kit workflow (`story-engineer`) before implementing.

## Phase 7 — Implementation

**Routing.** Implementation, refactoring and debugging are dispatched to
**`story-engineer`** (sonnet). The controller does not implement or debug: it
hands over the current task (or a contiguous group of tasks) plus the approved
plan path, the approved architectural decisions, and the anchors already
established; it receives a compact result and dispatches the next task.

The **approved** Squad Kit plan is the source of truth; the plan has already
been read in full, so the engineer works from the current task/section and
re-reads other parts only when evidence forces a re-evaluation. The engineer
invokes the relevant
installed Superpowers skills rather than a duplicate methodology — typically
`superpowers:using-git-worktrees` or a `feat/crm-<n>-<slug>` branch,
`superpowers:test-driven-development`, `superpowers:executing-plans`, and
`superpowers:systematic-debugging` for any failure.

Implement only the current story. Never implement a downstream story early —
if the plan's non-goals name it, it stays unimplemented. Implementation-level
failures are the engineer's to fix autonomously; an `ESCALATION REQUIRED` brief
recommending Opus sends the controller back to Phase 6 with that brief and its
anchors — the investigation behind it is not redone.

While implementing, the engineer runs only the tests/checks relevant to the task
it just changed — the complete plan-required verification runs once at Phase 8.

## Phase 8 — Verification (Completion Gate)

**Routing.** The controller enumerates the complete required check list from the
approved plan and dispatches **`verify-runner`** (haiku) to execute it. The
controller does not run the suite itself and does not re-run checks the runner
already ran. The controller decides whether this gate passes.

Once the implementation is stable, run the **complete** required verification
exactly once — every verification the approved plan specifies, plus, as
applicable:
build; lint/format; unit tests; integration tests; architecture tests;
migration/infrastructure checks; dependency-boundary checks; each Acceptance
Criterion; each Business Rule; scope exclusions; no secrets committed; no
debug/temp code left; `git diff` and `git status`. Use targeted git commands
during implementation; read the full diff here and at the Publication Gate,
where it matters.

`superpowers:verification-before-completion` binds the runner and the
controller alike: **never state that a command passed unless it was actually run
and its output read.** A pass is recorded compactly as
`<command> → PASS (<key counts/evidence>)`; failure output is preserved in full.
The runner never interprets a non-obvious failure — it returns an
`ESCALATION REQUIRED` brief recommending Sonnet.

Required correctness verification is never reduced. During implementation only
targeted checks run; here the complete story-required verification runs **once**
against the stable final state. Do not re-run the entire suite after every
small change.

An implementation defect: the controller routes the failure brief and its
preserved output to **`story-engineer`** to fix, then re-dispatches
`verify-runner` for the affected checks. Rerun the full suite when the fix could
affect broader behavior, and always for the final completion evidence — the
story is never reported complete on partial reruns. A fix that would require
changing approved architecture: `story-engineer` escalates to `arch-reviewer`
and the controller stops at Phase 6.

## Phase 9 — Independent review

**Routing.** Dispatch **`story-reviewer`** (sonnet, read-only) from fresh
context. It reviews the **entire final story state**, not only the latest
changes, and reads the full final diff — the controller does not perform this
review itself. An architectural concern comes back as an `ESCALATION REQUIRED`
brief for **`arch-reviewer`** (opus) and, if it is a real architectural/product
decision, re-enters Phase 6; the reviewer never resolves it.

Review the finished work fresh (`superpowers:requesting-code-review`) against:
(1) the Linear story, (2) Acceptance Criteria, (3) Business Rules, (4) the
approved plan, (5) ADRs, (6) architecture boundaries, (7) scope exclusions,
(8) security/secrets, (9) dependency direction, (10) tests and verification
evidence. Implementation-level findings are routed to **`story-engineer`** to
fix, then the affected verification is re-dispatched to `verify-runner`. Apply
`superpowers:receiving-code-review` — verify a finding before acting on it.

Review depth is risk-routed, and the review itself is mandatory at every level:

- **STANDARD** — one `story-reviewer` (sonnet) final review.
- **ARCHITECTURE** — the Phase 5 Opus architecture review before
  implementation, plus one `story-reviewer` (sonnet) final code review here.
- **HIGH_RISK** — the existing full review behavior is retained, including Opus
  involvement wherever architecture or security judgment is materially useful.

This independent review happens once over the final code state; **never run
multiple reviews against an unchanged code state**, and do not run duplicate
reviews of the same unchanged code. The controller decides whether the review
gate passes.

## Phase 10 — Completion report

The **controller** writes this report directly from the workflow state and the
agents' compact returns — it is a small mechanical operation, so there is no
reporter agent. Before any Git publication, report — concisely. Successful items are one line:
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

The controller requests this approval and performs the publication itself —
staging, commit, push, PR and the Linear write are small mechanical operations,
so there is no linear-publisher agent, and no agent may publish.

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

## Gates that are never removed

At every risk level, Workflow V2 preserves: dependency/blocker validation;
reconciliation with existing implementation; never overwriting partial work;
never regenerating an approved plan unnecessarily; Acceptance Criteria
verification; Business Rules verification; the relevant architecture and
security checks; the Completion Gate; an independent final review; explicit
Publication approval; staged-diff inspection; no force push; never marking an
issue Done merely because a PR exists; and **STOP after one story**.

Risk routing may reduce ceremony — an automatic Opus pass, an empty Decision
Gate, a duplicated review, an oversized plan. It may never reduce a gate above.

## Safety rules

Never: trust Linear state without repository reconciliation; overwrite a
partial implementation; regenerate an approved plan unnecessarily; change
architecture silently; expand scope silently; rewrite an ADR unless the
approved plan explicitly requires it; include unrelated working-tree changes;
commit generated or runtime artifacts; force-push; mark an issue Done because
implementation exists; continue automatically to another story; rely on
slash-command `model:` frontmatter for model selection; let an agent decide a
gate, an architectural/product decision or a publication; redo an investigation
that an escalation brief already established; present an empty Decision Gate;
review an unchanged code state twice; add structural complexity that fails the
Phase 2c YAGNI Gate; weaken security, authorization, transaction,
secret-management or data-integrity verification for speed or token cost.

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
