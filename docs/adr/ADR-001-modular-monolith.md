# ADR-001 --- Modular Monolith Boundaries

**Status:** Accepted baseline.

## Decision

Use an ASP.NET Core modular monolith with explicit module ownership,
allowed dependency directions, public contracts and no direct
cross-module persistence access.

## Rule

Story plans may refine details but must not silently contradict this
ADR.
