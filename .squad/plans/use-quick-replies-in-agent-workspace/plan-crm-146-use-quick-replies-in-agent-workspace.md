# Plan — CRM-146 Use Quick Replies in Agent Workspace

See the intake's Scope note for the two Decision Gate resolutions this plan
implements: target the internal note composer (CRM-147), and add a minimal
allow-listed variable catalog (no Channel/Department).

## Backend — extend QuickReplyManagement (no new module)

`src/backend/src/Modules/QuickReplyManagement/SquadCrm.Modules.QuickReplyManagement/`

- New endpoint `POST /api/v1/quick-replies/{id}/resolve`
  (`Presentation/Endpoints/QuickReplyManagementEndpoints.cs`), auth
  `PermissionPolicies.QuickRepliesView` (same floor as read/list — resolving
  is a read operation).
- `Presentation/Requests`: `ResolveQuickReplyRequest(Guid? TicketId)`.
- `Presentation/Responses`: `ResolvedQuickReplyResponse(string? ArabicContent,
  string? EnglishContent, IReadOnlyList<string> UnresolvedVariables)`.
- `QuickReplyService.ResolveAsync(id, ticketId, cancellationToken)`:
  - Load via the existing `FindVisibleAsync`+`IsActive` gate (same visibility
    as `GetAsync`); inactive or invisible → `NotFound` (existing failure
    enum member — no new one needed there).
  - Build the variable map:
    - `AgentName` — `IStaffDisplayNameReader.GetAsync(callerId)` (new, see
      below), falls back to the caller's email if no display name is set.
    - `TicketNumber`/`CustomerName` — only when `TicketId` is supplied:
      check `permission:tickets.view` via a small internal authorizer
      mirroring `GlobalQuickReplyAuthorizer` exactly (string-policy check,
      no project reference to TicketManagement's presentation layer); on
      failure, treat both tokens as unresolved rather than 403 — a resolve
      call is inserting draft text, not reading the ticket record itself, so
      failing soft (leave `{{TicketNumber}}` literal) matches "surfaced
      safely" better than hard-failing the whole action. Then
      `ITicketReferenceReader.GetAsync(ticketId)` (new, see below) for
      `TicketNumber`+`CustomerId`, then `ICustomerNameReader.GetAsync(customerId)`
      (new) for the display name (`FirstName LastName`).
  - Substitute only the three allow-listed tokens via a fixed regex
    (`{{\s*(CustomerName|TicketNumber|AgentName)\s*}}`) independently against
    `ArabicContent` and `EnglishContent` (either may be null — substitute
    only the non-null one(s)). Any token match whose value could not be
    resolved is left as literal text and its name added to
    `UnresolvedVariables` (deduplicated). No other `{{...}}`-shaped text is
    touched — an unsupported token is never matched by the allow-listed regex
    class, so it always passes through unresolved and reported, never
    treated as code/expression (keeps CRM-145's "no arbitrary expression
    evaluation" rule intact).

### New tiny cross-module contracts (mirror `ITicketExistsLookup` exactly)

- `TicketManagement.Contracts/ITicketReferenceReader.cs`:
  `Task<TicketReference?> GetAsync(Guid ticketId, CancellationToken)`,
  `record TicketReference(string TicketNumber, Guid CustomerId)`. Impl
  `TicketManagement/Application/Services/TicketReferenceReader.cs` (EF
  no-tracking projection), registered in `TicketManagementModule.cs`.
- `CustomerManagement.Contracts/ICustomerNameReader.cs`:
  `Task<CustomerName?> GetAsync(Guid customerId, CancellationToken)`,
  `record CustomerName(string FirstName, string LastName)`. Impl in
  `CustomerManagement/Application/Services/CustomerNameReader.cs`, registered
  in `CustomerManagementModule.cs`.
- `StaffIdentity.Contracts/StaffSubjectReference.cs`: add `string?
  DisplayName` to the existing record (additive; only
  `StaffSubjectReferenceReader.cs`'s two `Select` projections need the extra
  field — no other consumer touches the third position). `AgentName` becomes
  `DisplayName ?? emailLocalPart` — reuse `FindByIdAsync`, no new contract
  method needed.
- `QuickReplyManagement.csproj`: add `ProjectReference`s to
  `TicketManagement.Contracts`, `CustomerManagement.Contracts`,
  `StaffIdentity.Contracts` (Contracts projects only, per module-boundary
  rule 11/12).
- New internal `ITicketAccessAuthorizer`/`TicketAccessAuthorizer` in
  QuickReplyManagement's `Infrastructure/Authorization/`, structurally
  identical to `GlobalQuickReplyAuthorizer` but checking
  `"permission:tickets.view"` by string (no TicketManagement project
  reference needed for this — policy strings are the established
  cross-module contract per RoleManagement's central registration).

No migration needed (no new persisted fields — everything resolved is either
a lookup of already-persisted data, or the display name addition to an
existing DTO/record, not a table).

## Frontend — insert into the existing note dialog

`src/frontend/projects/agent-crm/src/app/tickets/`

- `quick-replies.service.ts` (or a light addition — check whether it's
  reused from `../quick-replies/` or duplicated per CRM-145's existing
  location): add `resolve(id, ticketId): Promise<ResolvedQuickReply>`
  calling the new endpoint. `ResolvedQuickReply { arabicContent,
  englishContent, unresolvedVariables }`.
- `ticket-detail.ts`:
  - `quickReplyOptions = signal<{label,value}[]>([])`, loaded alongside
    `loadAgentOptions()` from `QuickRepliesService.list(1, 100)` (reuse
    CRM-145's already-scope-filtered, `activeOnly` list — add an
    `activeOnly` param support if the service method doesn't already take
    one; it does, per the `QuickReplyListQuery` filters on the backend — add
    the query param client-side if missing).
  - `selectedQuickReplyId` control (not part of `noteForm` — a transient
    picker, not submitted data).
  - `insertQuickReply()`: calls `resolve(id, ticket().id)`, picks
    Arabic/English content by `localization.locale()` (falling back to the
    other language if the selected one is null — mirror `localizedName`'s
    fallback pattern), appends/sets `noteForm.controls.body`, and sets a new
    `quickReplyWarningKey` signal when `unresolvedVariables.length > 0` for
    an inline `p-message severity="warn"` under the picker.
  - Reset `quickReplyWarningKey`/`selectedQuickReplyId` in `startAddNote()`
    and `cancelAddNote()` alongside the existing form reset.
- `ticket-detail.html`: inside the note `p-dialog` (`ticket-detail.html:594`
  block), above the body textarea, add a `p-select` (mirrors the existing
  watcher-picker filterable select at line ~668) bound to
  `quickReplyOptions()` with a "Insert" button (`(onClick)="insertQuickReply()"`),
  plus the warning `p-message` when set. No new translation *file* — add keys
  to the existing `tickets` translation namespace (`tickets.notes.quickReply.*`).

## Tests

- Backend: extend `QuickReplyEndpointsAuthorizationTests` with the new
  `/resolve` route (401 anonymous). New
  `QuickReplyManagementTests` (or a focused new test class) covering:
  variable substitution with a ticket id, without a ticket id (ticket-scoped
  tokens unresolved), unknown-token pass-through, inactive/invisible
  template → not-found, `tickets.view`-denied caller → ticket tokens
  unresolved (not 403).
- Frontend: extend `ticket-detail.spec.ts` (or add a focused spec) for
  insert-then-edit-then-submit, and the unresolved-variable warning path.

## Verification

- Backend: `dotnet build`, affected test projects
  (`SquadCrm.Api.Tests`, `SquadCrm.Persistence.IntegrationTests` if touched,
  QuickReplyManagement-focused tests), architecture tests (new
  cross-module contract references must stay Contracts-only).
- Frontend: affected specs, `ng lint`/build.
- Browser smoke: open a ticket, add a note, insert a quick reply with and
  without a resolvable ticket variable, confirm literal `{{Token}}` + warning
  on the unresolved path, confirm nothing sends until "Add note" is clicked,
  spot-check Arabic locale.
