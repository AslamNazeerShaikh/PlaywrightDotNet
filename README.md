# PlaywrightDotNet — JobLens

> **Status:** production ready back-end API and ops-deck cockpit front-end.
>
> Polite, low-volume job-board automation on .NET 10. Playwright collects from Naukri, Indeed India, and opt-in LinkedIn into SQLite, with skill-demand analysis and hyperlinked Excel snapshots, plus a live ops-deck cockpit.
> A modular monolith built to be read: explicit use-case handlers, 971 behavior-pinned tests, and docs that match the tree.

## Architecture

<img src="docs/explicit-architecture.svg" width="100%" alt="Ports-and-adapters architecture: control plane, application core, and driven adapters" />

## Screenshots

### 01 Overview

Central dashboard. Engine state, proxy latency, tac-map, endpoint catalog, and killfeed.

<div style="display:flex;gap:12px;">
<img src="docs/screenshots/01-overview-light.png" width="49%" alt="Overview page in light theme" />
<img src="docs/screenshots/01-overview-dark.png" width="49%" alt="Overview page in dark theme" />
</div>

### 02 Engine Health

Probe control, full health payload, latency stats, and sample history.

<div style="display:flex;gap:12px;">
<img src="docs/screenshots/02-health-light.png" width="49%" alt="Engine Health page in light theme" />
<img src="docs/screenshots/02-health-dark.png" width="49%" alt="Engine Health page in dark theme" />
</div>

### 03 Search Profiles

Create and edit search profiles with inline server field errors. Persisted in SQLite.

<div style="display:flex;gap:12px;">
<img src="docs/screenshots/03-profiles-light.png" width="49%" alt="Search Profiles page in light theme" />
<img src="docs/screenshots/03-profiles-dark.png" width="49%" alt="Search Profiles page in dark theme" />
</div>

### 04 Sources

One card per job source. Compliance gate, daily quota meters, policy caps, circuit state.

<div style="display:flex;gap:12px;">
<img src="docs/screenshots/04-sources-light.png" width="49%" alt="Sources page in light theme" />
<img src="docs/screenshots/04-sources-dark.png" width="49%" alt="Sources page in dark theme" />
</div>

### 05 Sessions

Browser-profile sessions. One identity per source, launch readiness at a glance.

<div style="display:flex;gap:12px;">
<img src="docs/screenshots/05-sessions-light.png" width="49%" alt="Sessions page in light theme" />
<img src="docs/screenshots/05-sessions-dark.png" width="49%" alt="Sessions page in dark theme" />
</div>

### 06 Runs

Start a scrape (optionally new-only), follow it live, cancel it, and read postings with CSV and XLSX export. NEW ONLY badge, inline control tips.

<div style="display:flex;gap:12px;">
<img src="docs/screenshots/06-runs-light.png" width="49%" alt="Runs page in light theme" />
<img src="docs/screenshots/06-runs-dark.png" width="49%" alt="Runs page in dark theme" />
</div>

### 07 Topology

Request lifecycle sequence, component map, layer contract, plus the current scrape flow and the new-only delta.

<div style="display:flex;gap:12px;">
<img src="docs/screenshots/07-topology-light.png" width="49%" alt="Topology page in light theme" />
<img src="docs/screenshots/07-topology-dark.png" width="49%" alt="Topology page in dark theme" />
</div>

### 08 Database

Browse every user table in the SQLite store. Live schema, paged rows, CRUD, live diagram.

<div style="display:flex;gap:12px;">
<img src="docs/screenshots/08-database-light.png" width="49%" alt="Database page in light theme" />
<img src="docs/screenshots/08-database-dark.png" width="49%" alt="Database page in dark theme" />
</div>

### 09 Users

Signed-in account, password change, plus admin user list, create, and role assignment.

<div style="display:flex;gap:12px;">
<img src="docs/screenshots/09-users-light.png" width="49%" alt="Users page in light theme" />
<img src="docs/screenshots/09-users-dark.png" width="49%" alt="Users page in dark theme" />
</div>

### 10 Roles

Role catalog with per-role permission grants across the permission keys.

<div style="display:flex;gap:12px;">
<img src="docs/screenshots/10-roles-light.png" width="49%" alt="Roles page in light theme" />
<img src="docs/screenshots/10-roles-dark.png" width="49%" alt="Roles page in dark theme" />
</div>

### 11 Quotas

Usage quota definitions with per-user limits and fixed windows.

<div style="display:flex;gap:12px;">
<img src="docs/screenshots/11-quotas-light.png" width="49%" alt="Quotas page in light theme" />
<img src="docs/screenshots/11-quotas-dark.png" width="49%" alt="Quotas page in dark theme" />
</div>

## Verified stack

Status means In use (referenced by a project today) or Planned (design-matrix version, no package reference yet).

| Area | Package | Version | Status | What it does |
| ---- | ------- | ------- | ------ | ------------ |
| Runtime | .NET SDK / runtime | 10.0.403 / 10.0.14 | In use | LTS base. SDK pinned in `global.json`. Every project targets `net10.0`. |
| Tests | xUnit.net v3 + VS adapter | 4.0.3 / 4.0.1 | In use | Test framework, assertions, and the Visual Studio runner across all test projects. |
| Browser automation | `Microsoft.Playwright` | 1.64.1 | In use | Headed browsing with persistent per-profile contexts. Drivers for Chromium 156, Firefox 157, WebKit 27.2. |
| Data access | EF Core SQLite + `Microsoft.Extensions` | 10.0.14 | In use | SQLite storage through EF Core. Options binding and DataAnnotations validation through Extensions. |
| Raw explorer access | `Microsoft.Data.Sqlite` | 10.0.14 | In use | Direct SQLite access for the raw database explorer. |
| Raw explorer mapping | Dapper | 2.1.89 | In use | Micro-ORM for the raw database explorer. Contract unchanged. |
| HTML parsing | AngleSharp | 1.10.1 | In use | Offline parsing of captured search and detail pages. |
| Excel export | ClosedXML | 0.105.1 | In use | XLSX export of postings. Referenced by Infrastructure. |
| Logging | Serilog + sinks and enrichers | 10.0.0 | In use | Compact JSON console and file logs. 90-day retention from validated options. |
| CLI parsing | `System.CommandLine` | 2.0.14 | Planned | Future command-line surface. No package reference exists today. |
| HTTP resilience | Polly v8 via `Microsoft.Extensions.Http.Resilience` | 10.0.0 | Planned | Future retry policy. No package reference exists today. |
| API reference | `Microsoft.AspNetCore.OpenApi` | 10.0.14 | In use | OpenAPI document generated and served in Development. |
| API docs UI | `Scalar.AspNetCore` | 2.18.5 | In use | Local interactive reference at `/scalar`. Development only. |

## What this project will be

A modular monolith + CLI.

| Capability | How it works |
| ---------- | ------------ |
| Search | Playwright drives public job pages with a persistent, headed browser profile. One identity per source. |
| Collect | Extract, validate, and deduplicate postings, then store them in SQLite. |
| Enrich and export | Optionally visit detail pages, then export CSV and Excel. |
| Anti-bot | Stop, cool down, and ask a human at walls. No CAPTCHA solving, fingerprint spoofing, auth-wall bypass, or proxy rotation. |
| Application style | Explicit use-case handlers with no mediator. Expected failures travel as `Result<T>`. Options validated at startup. Reusable query criteria as Specifications. API errors as Problem Details. |
| Library use | Each package in its documented, recommended configuration. AOT and trimming are not design constraints. |
| LinkedIn | High-risk source. Disabled by default. |

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
17. [Library usage policy](docs/16-library-usage-policy.md)
18. [ADRs](docs/10-adr/README.md)
19. [Feature toggles](docs/21-feature-toggles.md)

## Codebase stats (tokei)

Measured with [tokei](https://github.com/xampprocky/tokei) at the repo root. Build outputs stay out through `.gitignore`. Regenerate with `tokei .`.

```text
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 Language              Files        Lines         Code     Comments       Blanks
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 C#                      439        56910        41227         9947         5736
 CSS                       1         1738         1413          103          222
 JSON                      8       506356       506356            0            0
 MSBuild                  13          371          313           37           21
 NuGet Config              1            7            7            0            0
 Shell                     1           59           46            2           11
 SVG                       2          211          162            8           41
 TOML                      1           14            9            4            1
 TSX                      34        11035         9483          884          668
 TypeScript               23         3435         2950          144          341
─────────────────────────────────────────────────────────────────────────────────
 HTML                     20         1042         1018            8           16
 |- CSS                    7           44           44            0            0
 |- JavaScript             5          281          266           13            2
 (Total)                             1367         1328           21           18
─────────────────────────────────────────────────────────────────────────────────
 Markdown                 63        17163            0        13352         3811
 |- BASH                   6          273          173           59           41
 |- CSS                    1           19           11            4            4
 |- Dockerfile             1            8            4            4            0
 |- JSON                   7          270          270            0            0
 |- PowerShell             2           30           26            1            3
 |- TOML                   1           21           15            4            2
 |- XML                    3           95           93            0            2
 |- YAML                   2           60           55            2            3
 (Total)                            17939          647        13426         3866
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 Total                   606       599442       563941        24576        10925
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

```
