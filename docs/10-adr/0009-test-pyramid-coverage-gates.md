# ADR-0009: Test Pyramid + Risk-Based Coverage Gates

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** Project owner

## Context

The system combines deterministic logic (URL builders, parsers, policies) with nondeterministic browser/site behavior. Live-site tests are slow, flaky, rate-limited, and legally risky in CI. The old project used a 100% coverage gate; that is expensive and does not prove behavior. We need fast feedback and confidence in the failure modes that matter (challenge, rate limit, dedupe, cancellation).

## Decision

Use a **test pyramid**:

- Many domain/application unit tests (no I/O).
- Parser tests with saved HTML fixtures (no browser).
- Adapter contract tests (no browser/network).
- SQLite integration tests (real file, no server).
- A small Playwright E2E suite against a **local fixture server**.
- Live canaries are manual/nightly, opt-in, never in CI.

Coverage gates are risk-based: Domain ≥ 95/90, Application ≥ 90/85, overall ≥ 85/80. No 100% target.

## Consequences

### Positive

- Fast, deterministic CI.
- Failure modes are tested without live sites.
- Browser tests focus on integration, not every branch.
- Coverage reflects risk, not vanity.

### Negative / trade-offs

- Fixtures can drift from real sites; canaries/quarterly refresh are required.
- Some browser behavior is only caught in E2E/live.
- Contract tests require adapter seams (parser/URL builder) to be testable offline.

### Follow-ups

- Keep fixtures minimal and synthetic.
- Quarantine flaky tests with an owner/expiry.
- Run the browser matrix nightly.
- Add mutation spot checks for Domain if time allows.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| 100% coverage | Slow, brittle, incentivizes low-value tests. |
| Test-after only | Invariants/policies are easy to get wrong; TDD is cheaper. |
| Live-site CI | Flaky, rate-limited, legal/ethical risk. |
| E2E for everything | Slow and hard to diagnose; pyramid inverted. |
| No browser E2E | Misses integration/wait/selector bugs. |

## References

- [Testing Strategy](../06-testing-strategy.md)
- [Gameplan](../05-gameplan.md)
