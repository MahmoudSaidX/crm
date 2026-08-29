# Story intake — CRM-201

## Feature

- **Feature:** OpenTelemetry, Structured Logging & Health Checks
- **Tracker:** Linear `CRM-201`
- **Status:** In Progress
- **Milestone:** Sprint 0 — Project Setup
- **Priority / estimate:** High / 3 points

## Requirements

- Structured logs include safe correlation and trace context.
- Configure OpenTelemetry tracing/metrics for HTTP, PostgreSQL and background work where supported.
- Add liveness/readiness checks for critical dependencies.
- Keep sensitive data out of logs, traces, metrics and health responses.
- Make OTLP export environment-configurable.
- Surface pending/failed/exhausted outbox counts and safe failure diagnostics.

## Dependencies and evidence

- CRM-199, CRM-204, CRM-105 and CRM-197 are Done and merged on `origin/main`.
- CRM-198/199 provide the module-owned outbox, bounded error type, retry state and Hangfire job.
- ADR-008 requires structured logging, OpenTelemetry, correlation propagation, health checks and redaction.
- CRM-204 distinguishes client/support `correlationId` from OpenTelemetry `traceId`; never collapse them.

## Scope boundary

- Use stable OpenTelemetry 1.18 packages and Npgsql.OpenTelemetry 10.0.3, matching the repository's Npgsql version.
- Export only when standard `OTEL_EXPORTER_OTLP_ENDPOINT` is configured.
- Report outbox counts and exception type only. Never read/export payload or durable error text.
- Do not add dashboards, collectors, alerting, business SLAs, CRM-202 CI/test infrastructure, or vendor-specific domain dependencies.
