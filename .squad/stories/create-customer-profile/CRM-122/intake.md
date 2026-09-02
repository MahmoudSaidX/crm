# CRM-122 — Create Customer Profile

## Story
As a support user, I want to create a customer profile so that support
activity can be associated with a known customer.

## Acceptance Criteria
- Authorized user can create a customer with valid identity/profile data.
- System generates a unique customer identifier.
- Duplicate safeguards check configured identity/contact fields before creation.
- Customer is created within the user's permitted organizational scope.
- Creation is audited.

## Business Rules
- Customer identifier is system-generated and immutable.
- Duplicate detection warns/blocks according to configured matching policy; it must not silently merge records.
- Inactive department/branch references cannot be selected for new customers.
- Authorization is enforced server-side.

## Fields
| Field | Type | Required | Rules |
| -- | -- | -- | -- |
| CustomerNumber | string | System | Unique, immutable |
| FirstName | string | Yes | Trimmed, non-empty |
| LastName | string | Yes | Trimmed, non-empty |
| PreferredLanguage | enum | No | Arabic, English |
| DepartmentId | UUID | No | Active/permitted department |
| BranchId | UUID | No | Active/permitted branch |
| Status | enum | System | Active by default |

## Scope note
Deadline override: build only the simplest complete Create behavior. New
`CustomerManagement` module, same schema-per-module shape as
`BranchManagement`/`DepartmentManagement`, substituting Create-only for this
story (no edit/list/view/deactivate endpoints — those belong to CRM-123/124/125).
Duplicate detection checks FirstName+LastName+DepartmentId+BranchId combination
(no separate contact-detail fields exist yet — those arrive in CRM-126).
Organizational scope = DepartmentId/BranchId must reference active
department/branch records (mirrors the existing "inactive ref rejected"
Business Rule pattern from Branches/Departments); no new caller-scoped
visibility filtering is introduced since no read/list endpoint exists in
this story.
