# ADR-0008: CLI First, API/Dashboard Later

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** Project owner
- **Refined by:** [ADR-0010](0010-react-bun-cockpit.md) — the API becomes the first v2.0 deliverable because the cockpit depends on it; v1 remains CLI-only.

## Context

The primary user is one developer on one machine. The first milestone is to prove the collection pipeline on a real source. An ASP.NET Core API/dashboard would add endpoints, auth, hosting, CORS, UI, and tests before the core is proven. The owner knows ASP.NET Core, so adding it later is low-risk.

## Decision

Ship a **CLI first** (`System.CommandLine` + Hosting + Serilog). Defer the API/dashboard until after the Naukri vertical slice (Phase 3) and only if a real need appears. Reuse the same Application handlers when the API is added.

## Consequences

### Positive

- Smallest useful surface; faster to ship.
- Easy scheduling via OS tools.
- Logs/artifacts are first-class.
- No auth/hosting/UI scope creep.

### Negative / trade-offs

- No browser UI; queries are CLI/SQL/Excel.
- Remote access requires SSH or a later API.
- Visual progress is limited to console/logs.

### Follow-ups

- Keep Application independent of the CLI.
- Add read-only API endpoints first if needed.
- Do not put scraping logic in controllers.
- Use a hosted service/queue for long runs if an API is added.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| API-first | Delays the pipeline; extra surface before validation. |
| Desktop UI | Cross-platform UI overhead; not needed. |
| Daemon/scheduler in-process | More lifecycle/health complexity; OS scheduler is enough. |
| Notebook/script | Poor structure/testability for a long-lived tool. |

## References

- [Architecture](../02-architecture.md)
- [Gameplan](../05-gameplan.md)
