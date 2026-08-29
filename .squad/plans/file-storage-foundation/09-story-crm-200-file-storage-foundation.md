# Story 09 — File Storage Foundation (CRM-200)

## Goal and boundaries

Add the smallest provider-neutral file-storage boundary and a safe local adapter. Owning business modules will authorize access and persist `FileReference` metadata when they arrive. CRM-200 exposes no HTTP endpoint and creates no shared attachment table because no current entity can own or authorize an attachment.

## Implementation

1. Add dependency-free storage contracts and the exact Linear `FileReference` fields under `BuildingBlocks.Abstractions`.
2. Add `Infrastructure.FileStorage` with validated options, an extensible upload-validator seam, and a local filesystem adapter.
3. Generate opaque storage keys; canonicalize and containment-check every resolved path; never use original filenames as paths; reject invalid sizes, MIME types, unsafe keys, truncated streams and oversized streams.
4. Write through a temporary file and atomically move on success; remove partial files on failure. Reads are read-only and deletes are idempotent.
5. Register the selected provider in the API composition root through configuration. Only `Local` is supplied by this story; unsupported provider names fail fast and a future adapter can replace the registration without changing modules.
6. Add focused tests for upload/read/delete, metadata separation, validation, traversal resistance and cleanup.
7. Document configuration, authorization responsibility and deferred capabilities.

## Verification

- `dotnet build src/backend/SquadCrm.sln --no-restore`
- `dotnet test src/backend/tests/SquadCrm.Api.Tests/SquadCrm.Api.Tests.csproj --no-restore`
- `dotnet test src/backend/tests/SquadCrm.ArchitectureTests/SquadCrm.ArchitectureTests.csproj --no-restore`
- Review every CRM-200 acceptance criterion/business rule and final diff; confirm no endpoint, business module, database migration, cloud provider or downstream-story work was added.
