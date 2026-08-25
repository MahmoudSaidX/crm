# ADR-006 --- Background Processing

**Status:** Accepted baseline.

## Decision

Use Hangfire for background/delayed/recurring execution. Business state
stays in domain/application models; retries are idempotent.

## Rule

Story plans may refine details but must not silently contradict this
ADR.
