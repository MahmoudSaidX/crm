# Story intake — CRM-200

## Feature

- **Feature:** File Storage Foundation
- **Plan slug:** `file-storage-foundation`
- **Tracker:** Linear `CRM-200`
- **Status:** In Progress
- **Milestone:** Sprint 0 — Project Setup
- **Priority / estimate:** High / 3 points

## Story

As a developer, I want a storage abstraction for CRM attachments so that business modules can store files without depending on one physical provider.

## Acceptance criteria

- Provide provider-neutral upload, read/download and delete operations.
- Configure a safe local-development provider without changing business modules.
- Keep metadata separate from file bytes.
- Provide file-size/type validation hooks and secure access patterns.
- Make the provider replaceable through configuration/DI.

## Business rules and fields

- Business modules own authorization and persist provider-neutral file references; the storage adapter never authorizes an owning CRM resource.
- Original filenames are display metadata only and never become filesystem paths.
- `FileReference`: `Id`, `StorageKey`, `OriginalFileName`, `ContentType`, `SizeBytes`, `CreatedAtUtc`, `CreatedBy`.

## Dependencies and evidence

- Blocked by CRM-204 and CRM-105; both are Done and present on `origin/main`.
- ADR-007 requires provider-neutral storage with validated/authorized references.
- ADR-001 keeps business persistence inside the owning module.
- No attachment-owning business module exists in Sprint 0. Do not invent one or a shared attachment database.

## Scope boundary

- Add a dependency-free contract/metadata model, validation seam, safe local filesystem adapter, configuration/DI registration, targeted tests and documentation.
- Do not add upload/download endpoints: an endpoint without an owning entity cannot perform the required resource authorization.
- Do not add customer/ticket attachments, cloud storage, image processing, malware scanning, public URLs, or CRM-202 test infrastructure.
