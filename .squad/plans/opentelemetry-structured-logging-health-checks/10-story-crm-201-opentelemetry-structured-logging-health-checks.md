# Story 10 — OpenTelemetry, Structured Logging & Health Checks (CRM-201)

## Goal

Add a vendor-neutral observability baseline around the existing HTTP, PostgreSQL and Hangfire/outbox execution paths, with safe health signals and optional OTLP export.

## Implementation

1. Add stable OpenTelemetry 1.18 host, ASP.NET Core, HTTP, runtime and OTLP packages plus Npgsql.OpenTelemetry 10.0.3.
2. Configure resource identity, HTTP/runtime/PostgreSQL instrumentation, module outbox `ActivitySource`/`Meter`, and optional trace/metric/log OTLP export controlled by `OTEL_EXPORTER_OTLP_ENDPOINT`.
3. Add a structured request log scope containing distinct sanitized `CorrelationId`, `TraceId` and `SpanId` values.
4. Flow stored correlation IDs into the outbox job scope; add safe processing spans and processed/failed counters. Record only message ID, versioned event type and exception type—never payload/error content.
5. Preserve `/health` as liveness, add `/health/live`, and add `/health/ready` checks for PostgreSQL, configured local storage and module-owned outbox status.
6. Surface pending/failed/exhausted counts through readiness data. Return generic dependency failures without exception messages, SQL, paths or credentials.
7. Add focused tests and update configuration/documentation.

## Verification

- `dotnet build src/backend/SquadCrm.sln --no-restore`
- API tests, architecture tests and relevant persistence integration tests
- `dotnet format src/backend/SquadCrm.sln --no-restore --verify-no-changes`
- Review every CRM-201 acceptance criterion/business rule and final diff for telemetry leaks, secrets, architecture and downstream scope.
