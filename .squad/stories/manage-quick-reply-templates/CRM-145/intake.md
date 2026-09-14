# CRM-145 — Manage Quick Reply Templates

## Story

As an authorized support user, I want reusable quick reply templates so that
common customer responses can be prepared consistently and scoped to the right
users.

## Acceptance Criteria

- Authorized user can create, edit, view, list, activate and deactivate quick
  replies according to template scope permissions.
- Templates support Arabic and English content.
- Template scope can be Global or Personal with explicit owner metadata where
  required.
- Inactive templates remain historically identifiable but cannot be newly
  selected.
- Changes are audited.

## Business Rules

- Global templates require global-template administration permission; Personal
  templates belong to their owner.
- Template content is a draft aid and never sends a message by itself.
- Arbitrary executable expressions, scripts or unrestricted object/property
  traversal are not allowed in template content.
- Template scope restrictions are enforced by the backend, not only hidden in
  the UI.
- Deactivation is preferred to deletion after use/reference.

## Fields

| Field | Type | Required | Rules |
| -- | -- | -- | -- |
| QuickReplyId | UUID | System | Unique identifier |
| Name | string | Yes | Max 200; unique per scope (global set, or per owner) |
| ArabicContent | text | No | Max 4000; at least one language required |
| EnglishContent | text | No | Max 4000; at least one language required |
| Scope | enum | Yes | Global / Personal |
| OwnerUserId | UUID | Conditional | Required for Personal scope, server-resolved |
| IsActive | boolean | Yes | Defaults true |
| CreatedAtUtc/UpdatedAtUtc | timestamp | System | Server generated |

## Scope note — deferred AC (per the story's own Deadline Acceptance Override)

The Linear description opens with a Deadline Acceptance Override declaring
"Department/channel scoping, variable catalogs, template governance/versioning
and advanced permission policy" to be stretch/non-blocking, and personal/global
scope in scope "only if trivial". Personal/Global scope IS trivial here and is
built. Deferred, with reasons:

- **Department scope.** Not implementable today: StaffIdentity has no
  user→department association of any kind (`grep DepartmentId` over
  `Modules/StaffIdentity` returns nothing), so "permitted department scope"
  has no subject to evaluate. Building it would require inventing the
  organizational-scope model, which is a separate architectural concern.
- **Channel restriction.** No communication-channel concept exists yet — the
  channel catalog is Sprint 8 (CRM-163 Communication Channels). A `Channels`
  column today would be an enum with no defined members and no consumer.
- **Allow-listed variable catalog.** No variable/merge-field catalog exists.
  The underlying Business Rule ("no arbitrary executable expressions, scripts
  or object traversal") is satisfied instead by storing content as inert plain
  text that is never evaluated or interpolated by this story — nothing renders
  it as a template.

CRM-146 (Use Quick Replies in Agent Workspace) is the consumer and is where a
channel filter would first have a caller.
