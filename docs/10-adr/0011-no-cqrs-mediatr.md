# ADR-0011: Explicit Use-Case Handlers, No CQRS or MediatR

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** Project owner

## Context

JobLens has two entry points — the v1 CLI and the v2 HTTP API — and a small set of use cases: start a scrape run, cancel or resume a run, bootstrap a session, export jobs, and query read models. The domain already carries the real complexity: run lifecycle, quotas, compliance gates, challenge handling, and deduplication.

A mediator or CQRS framework would add runtime dispatch, assembly scanning, pipeline behaviors, and extra abstractions for an in-process application with one writer and one database. The project owner has explicitly chosen not to use CQRS or MediatR. The team is one developer, and debuggability and compile-time navigation matter more than handler decoupling.

The application still benefits from separating read shapes from write aggregates. That separation does not require a command/query bus.

## Decision

Use **explicit, constructor-injected use-case handlers** and **typed read-model ports**.

- Handlers are sealed classes with a `HandleAsync` method. They implement no framework interface.
- CLI commands and API endpoints call handlers directly.
- Reads use `IJobReadModel` / `ISkillDemandReadModel` methods that accept specifications or filters and return DTOs.
- Cross-cutting concerns use ASP.NET Core middleware, endpoint filters, DI decorators, or explicit calls — not mediator pipeline behaviors.
- Domain events are recorded by aggregates and handled explicitly after persistence; there is no mediator notification bus.
- No MediatR, `IRequest`/`IRequestHandler`, `IMediator`, `ICommand`/`IQuery`, command/query buses, or handler scanning.

The four supporting patterns are defined in [Application Patterns](../14-application-patterns.md):

- **Result** for expected operational failures.
- **Options** for validated, strongly typed configuration.
- **Specification** for composable query criteria.
- **Problem Details** for the API error contract.

## Consequences

### Positive

- A stack trace names the handler and the caller; no mediator frames.
- “Find usages” and navigation work at compile time.
- No assembly scanning, pipeline ordering, or hidden handler registration.
- Fewer packages and less startup magic.
- Handler tests instantiate the class and pass fakes; no mediator harness.
- The API can map `Result` to Problem Details in one place.

### Negative / trade-offs

- Shared behavior (validation, logging, transactions) must be applied explicitly, via middleware/filters/decorators, or it can be forgotten.
- A large number of use cases could make DI registration verbose. At this size, explicit registration is a feature.
- Handlers can drift toward god objects if orchestration is not kept thin; policies and adapters must carry domain/infrastructure detail.
- Read/write shape separation is a convention, not an enforced framework boundary.

### Follow-ups

- Add an architecture test that fails if `MediatR` or a banned namespace appears.
- Keep handler registration explicit; no assembly scanning.
- Review handlers at each phase exit for orchestration creep.
- Document new cross-cutting concerns in [Application Patterns](../14-application-patterns.md).

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| MediatR with pipeline behaviors | Adds runtime dispatch and hidden ordering for an in-process app; explicit calls are easier to trace and test. |
| Full CQRS + separate write/read models | The write model protects real invariants; reporting is simple. A bus and projection pipeline add cost without a scale requirement. |
| CQRS “lite” with command/query objects but no bus | Still adds ceremony and naming noise; request records and read-model methods are enough. |
| Inline handlers in minimal API endpoints | Duplicates orchestration between CLI and API; a shared use-case class keeps both entry points consistent. |
| Event sourcing | `RunEvent` is an audit trail, not a source of truth; overkill for this domain. |

## References

- [Application Patterns](../14-application-patterns.md)
- [Architecture §8](../02-architecture.md#8-ports-and-adapters)
- [Domain Model](../03-domain-model.md)
- [ADR-0004: Modular monolith + Clean Architecture](0004-modular-monolith-clean-architecture.md)
- [ADR-0008: CLI first, API later](0008-cli-first-api-later.md)
