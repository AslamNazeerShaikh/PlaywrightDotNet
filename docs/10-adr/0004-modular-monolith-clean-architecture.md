# ADR-0004: Modular Monolith + Clean Architecture + Focused DDD

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** Project owner

## Context

The system has one user, one machine, three sources, and a mix of complex rules (run lifecycle, session health, challenge policy, quotas, dedupe) and simple read-side work (export, skill counts). Microservices would add operational overhead with no scale requirement. A pure CRUD design would bury the rules in controllers/services. DDD everywhere would over-model reporting.

## Decision

Build a **modular monolith** with Clean Architecture layers:

- `Domain` — pure aggregates, value objects, policies, events.
- `Application` — use cases, ports, orchestration.
- `Infrastructure` — Playwright, EF Core, parsers, exporters.
- `Cli` — composition root.

Apply **DDD tactically** to Sourcing and Access; use read models/filter objects for Insight/Export. No CQRS, MediatR, AutoMapper, or generic repositories. See [ADR-0011](0011-no-cqrs-mediatr.md).

## Consequences

### Positive

- One deployable, one SQLite writer, one browser session — easy to reason about.
- Testable core without a browser/DB.
- Clear seams for adding sources, storage, or an API later.
- Avoids distributed-systems complexity.

### Negative / trade-offs

- Layer boundaries require discipline; architecture tests are mandatory.
- A monolith can become tangled if feature folders are not respected.
- More projects than a small script; justified by testability and growth.

### Follow-ups

- Enforce dependencies with NetArchTest.
- Keep source adapters isolated.
- Revisit only if multi-user/scale requirements appear.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| Microservices | No scale need; distributed transactions/locks/ops overhead. |
| Vertical slices only | Good for features, but run/session rules need a shared domain model. |
| CRUD + services | Rules leak into handlers; harder to test invariants. |
| DDD everywhere | Reporting is simple; aggregates add noise. |
| Event sourcing | `RunEvent` audit is enough; overkill. |

## References

- [Architecture](../02-architecture.md)
- [Domain Model](../03-domain-model.md)
