# ADR-0002: Use xUnit.net v3

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** Project owner

## Context

The project needs a test framework suitable for TDD, async tests, fixtures, parallel unit tests, and Playwright browser tests. xUnit v2 is in maintenance; xUnit v3 is the active line. Current `xunit.v3` is `4.0.2` (the v3 product line uses a 4.x package version), with VS adapter `4.0.1`. Playwright ships an official `Microsoft.Playwright.Xunit.v3` package.

## Decision

Use **xUnit.net v3** (`xunit.v3` `4.0.2`, `xunit.runner.visualstudio` `4.0.1`, `Microsoft.NET.Test.Sdk` `18.11.0`) for all test projects. Use `Microsoft.Playwright.Xunit.v3` `1.64.1` for browser E2E.

## Consequences

### Positive

- Active development and fixes (v2 is maintenance-only).
- Stand-alone test executables; fast in-process runner.
- Microsoft Testing Platform support; TRX/CTRF reporting.
- Assembly fixtures, `TestContext`, dynamic skip, `MatrixTheoryData`.
- Official Playwright xUnit v3 integration.

### Negative / trade-offs

- Newer runner/tooling; some older blog posts/patterns are v2-specific.
- Test projects must be executables (`OutputType=Exe`).
- `IAsyncLifetime` uses `ValueTask`; `async void` is rejected.
- VSTest vs MTP modes must be chosen consistently in CI.

### Follow-ups

- Pin versions; read release notes before upgrades.
- Document the chosen runner mode in CI.
- Disable collection parallelization for browser tests.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| xUnit v2 | Maintenance mode; new work goes to v3. |
| NUnit | Viable, but no strong advantage here; xUnit's TDD style and Playwright integration fit. |
| MSTest | Viable; Playwright support exists, but xUnit v3's fixtures/runner are preferred. |
| Custom harness | Reinvents discovery/parallelism/reporting. |

## References

- <https://xunit.net/>
- <https://xunit.net/docs/getting-started/v3/whats-new>
- [Testing Strategy](../06-testing-strategy.md)
