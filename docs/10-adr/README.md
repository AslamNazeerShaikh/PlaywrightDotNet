# Architecture Decision Records

> Lightweight ADRs capture **why** a decision was made, the alternatives, and the consequences. They are immutable once accepted; a later ADR supersedes an earlier one.

| ADR | Decision | Status |
|---|---|---|
| [0001](0001-dotnet-10-lts.md) | Target .NET 10 LTS | Accepted |
| [0002](0002-xunit-v3.md) | Use xUnit.net v3 | Accepted |
| [0003](0003-playwright-dotnet.md) | Use Playwright for .NET | Accepted |
| [0004](0004-modular-monolith-clean-architecture.md) | Modular monolith + Clean Architecture + focused DDD | Accepted |
| [0005](0005-sqlite-ef-core.md) | SQLite + EF Core 10 | Accepted |
| [0006](0006-browser-first-optional-api-fast-path.md) | Browser-first, optional API fast path | Accepted |
| [0007](0007-compliance-first-low-signal-automation.md) | Compliance-first low-signal automation | Accepted |
| [0008](0008-cli-first-api-later.md) | CLI first, API/dashboard later | Accepted |
| [0009](0009-test-pyramid-coverage-gates.md) | Test pyramid + risk-based coverage gates | Accepted |
| [0010](0010-react-bun-cockpit.md) | React + Bun web cockpit, no Node.js | Accepted |
| [0011](0011-no-cqrs-mediatr.md) | Explicit use-case handlers, no CQRS or MediatR | Accepted |
| [0012](0012-serilog-polly-runtime-hardening.md) | Serilog logging + Polly HTTP resilience runtime baseline | Accepted |
| [0013](0013-aot-friendly-policy.md) | AOT-friendly code, no NativeAOT release | Superseded by 0014 |
| [0014](0014-use-libraries-as-designed.md) | Use libraries as designed; AOT is not a constraint | Accepted |

## Conventions

- **Status:** Proposed / Accepted / Superseded / Deprecated.
- **Date:** decision date (ISO 8601).
- **Context:** forces and constraints, not a tutorial.
- **Decision:** the choice, in one or two sentences.
- **Consequences:** positive, negative, and follow-ups.
- **Alternatives:** what was rejected and why.

## Adding an ADR

1. Copy the format of an existing ADR.
2. Number sequentially (`0010-...`).
3. Link it from this index and from the relevant design doc.
4. If it supersedes another ADR, update both statuses.
