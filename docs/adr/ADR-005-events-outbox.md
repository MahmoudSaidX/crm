# ADR-005 --- Events & Transactional Outbox

**Status:** Accepted baseline.

## Decision

Separate domain events from versionable integration events; use
transactional outbox and idempotent consumers for durable async
integration.

## Rule

Story plans may refine details but must not silently contradict this
ADR.
