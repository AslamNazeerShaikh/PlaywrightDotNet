# PlaywrightDotNet — JobLens

> **Status:** planning complete; implementation has not started.
>
> A cross-platform .NET 10 job-board automation and public job-posting collector for Naukri, Indeed India, and (opt-in) LinkedIn.

## Start here

Read the documentation in this order:

1. [Documentation index](docs/README.md)
2. [Executive summary](docs/00-executive-summary.md)
3. [Research analysis](docs/01-research-analysis.md)
4. [Architecture](docs/02-architecture.md)
5. [Domain model](docs/03-domain-model.md)
6. [Solution & tooling](docs/04-solution-and-tooling.md)
7. [Gameplan](docs/05-gameplan.md)
8. [Testing strategy](docs/06-testing-strategy.md)
9. [Anti-bot & compliance](docs/07-anti-bot-and-compliance.md)
10. [Site playbooks](docs/08-site-playbooks.md)
11. [Operations runbook](docs/09-operations-runbook.md)
12. [Open decisions](docs/11-open-decisions.md)
13. [Web cockpit (v2)](docs/12-web-cockpit.md)
14. [Conventional Commits](docs/13-conventional-commits.md)
15. [Application patterns](docs/14-application-patterns.md)
16. [Runtime hardening](docs/15-runtime-hardening.md)
17. [ADRs](docs/10-adr/README.md)

## Verified stack

- .NET SDK **10.0.403** / runtime **10.0.14** (LTS through 2028-11-14)
- xUnit.net v3 **4.0.2** (VS adapter 4.0.1)
- Playwright for .NET **1.64.1** (Chromium 156, Firefox 157, WebKit 27.2)
- EF Core / Microsoft.Extensions **10.0.14**
- SQLite, System.CommandLine 2.0.14, AngleSharp, ClosedXML
- Serilog 10.0.0 (compact JSON file logs, enrichers, 90-day retention) + Polly v8 HTTP resilience via `Microsoft.Extensions.Http.Resilience` 10.0.0

## What this project will be

A **modular monolith + CLI** that:

- Searches public job pages with Playwright and a persistent, headed browser profile.
- Extracts, validates, deduplicates, and stores postings in SQLite.
- Optionally enriches details and exports CSV/Excel.
- Handles anti-bot walls by **stopping, cooling down, and asking a human** — no CAPTCHA solving, fingerprint spoofing, auth-wall bypass, or proxy rotation.
- Uses explicit use-case handlers — **no CQRS/MediatR** — with Result, Options, Specification, and Problem Details patterns.
- Treats LinkedIn as high-risk and disabled by default.

## Before implementation

Answer the decisions in [docs/11-open-decisions.md](docs/11-open-decisions.md). If unanswered, the recommended defaults apply and Phase 0 can start.

## Next step

Phase 0: solution skeleton, SDK/package pinning, CI, ADRs, and the first failing domain tests. See [docs/05-gameplan.md](docs/05-gameplan.md).
