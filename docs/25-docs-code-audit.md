# Docs-vs-Code Audit (2026-11-20)

Blind four-agent sweep. Each agent got one scope, no shared context, read-only, no builds or test runs. Nothing below was fixed. Direction UNKNOWN means neither side proves itself.

| # | Auditor | Scope | Findings |
|---|---|---|---|
| 1 | glm-5.3-flash#max | README, solution, gameplan, testing docs + solution layout | 21 |
| 2 | deepseek-v4.1-flash#max | Architecture, domain, ADRs, patterns, hardening, library policy + backend code | 42 |
| 3 | mimo-v2.6-flash | Anti-bot, playbooks, runbook, decisions, toggles + compliance code (retry after empty first attempt) | 15 |
| 4 | mimo-v2.6-flash | Cockpit, commits, harness docs + frontend code | 22 |

## HIGH (fix first)

| # | Claim | Reality | Evidence |
|---|---|---|---|
| H1 | README stack table: SDK 10.0.403, runtime 10.0.14, `net10.0` (README:119); EF Core + Data.Sqlite 10.0.14 (:122-123); AngleSharp 1.10.1 (:125); OpenAPI 10.0.14, Scalar 2.18.5 (:130-131) | SDK 11.0.100, TFM `net11.0`, EF Core 11.0.0, Data.Sqlite 11.0.0, AngleSharp 1.10.4, OpenAPI 11.0.0, Scalar 3.0.0 | `global.json:3`, `Directory.Build.props:3`, `Directory.Packages.props:15,17,43,46,47`, installed SDK `11.0.100`. Same README:5 already says .NET 11, so the file contradicts itself |
| H2 | "No implementation code yet" (docs/README:5, 00:19,247) | Full tree: 11 projects in `JobScrape.slnx`, 11 API controllers, ~1,100+ backend test methods | `ls`, controller dir listing |
| H3 | 5 controllers / 6 controllers / 17 handlers / 22 endpoints (docs/04 §2, docs/02:1044,:237) | 11 controllers, 39 `[Http...]` endpoints, 31 transient handlers + scoped `RunScrapeHandler` | `Controllers/` listing, `UseCaseRegistration.cs:81-144` |
| H4 | `RunEvent` entity + in-process dispatch after `SaveChangesAsync` (docs/03:317,531,538) | No `RunEvent` type in `src/`; `DomainEvents` ignored in config; only tests read the collection | `grep -r RunEvent src/` = 0 hits, `ScrapeRunConfiguration.cs:42` |
| H5 | `ErrorKind` = 12 values incl. Challenge/SourceChanged/Unexpected, no Unauthorized; "every code exists in `Errors`" with RateLimited/Storage undefined (docs/14:134-147,207-222) | 10 values incl. `Unauthorized`, none of those three; `RATE_LIMITED`, `STORAGE_LOW_DISK`, `ACCESS_DENIED` + 15 more codes exist | `ErrorKind.cs:13-59`, `Errors.cs:597,613,681`, `ErrorMapperTests` |
| H6 | Validation uses `ValidationProblemDetails`, no `errors` member, `retryAfter` on 429 (docs/14:397,415, docs/02:1000) | `ErrorMapper.cs:42` always calls `Results.Problem`, `:71` emits `errors` extension, `:58-79` emits no `retryAfter` | Code comment `:55` explains why |
| H7 | Retry backoff `min(base*2^attempt,max)`, 60 s cap, 0-20% jitter (docs/07:376-379) | `BaseDelay*4^(attempt-1)` (2/8/32 s), no cap, fixed 0-1 s jitter, max 2 attempts | `RetryPolicy.cs:21,26,84-90`, `PolicyTests.cs:121,132` |
| H8 | Browser launch defaults: viewport 1366x768/1440x900, SlowMo 40 ms, locale en-IN, timezone Asia/Kolkata (docs/07:194-197) | `ViewportSize.NoViewport`, no SlowMo/locale/timezone keys anywhere | `BrowserSessionPool.cs:88-91`, `BrowserOptions.cs:13-48`, grep 0 hits |
| H9 | Cockpit has 7 routes, auth/CSRF/bearer "not implemented", env table with `JOBLENS_*` tokens, ports 5173/5080, .NET E2E project (docs/12:1090,745,1044,141,1319; frontend README:4,316,330) | 11 routes through `#/quotas`; session auth + CSRF + bearer injection shipped and e2e-tested; envs are `DOTNET_API_URL`/`BFF_TLS_*`; ports 3000/5207/5208; Playwright suite in `src/tests/e2e/` | `router.ts:21-33`, `server/index.ts:29,116-127,271-293`, `proxy.ts:41-44`, `config.ts:17-29`, `package.json` |
| H10 | Naukri uses headed browser pagination via Next (docs/02:52-55,699) | `BrowserRegistration.cs:84-91` selects `NaukriApiJobSource`; `ApiOnly: true` default | `appsettings.json:73`, ADR-0006:6-9 |
| H11 | `Program.cs` service/middleware order diagram (docs/02:202) | Real pipeline adds rate limiting, auth, `IdempotencyMiddleware`, `UsageQuotaMiddleware`, `MapMcp("/mcp")` — none in the diagram | `Program.cs:69,108-134` |

## MEDIUM (fix second)

| # | Claim | Reality |
|---|---|---|
| M1 | One migration `20261107112658_InitialCreate`, 6 tables (docs/02:335,886, docs/04:712) | 3 migrations; InitialCreate has 11 `CreateTable` calls |
| M2 | Six `IEntityTypeConfiguration`, one FK `scrape_runs.profile_id` (docs/03:658,746) | 10 config files; exactly two FKs (docs/02:880 agrees with code) |
| M3 | `ISessionManager`/artifact ports without implementations, no read models (docs/03:643,648) | `PlaywrightSessionManager`, `FileArtifactStore`, three `*ReadStore` exist |
| M4 | `SESSION_UNAVAILABLE`, all `SESSION_*` as 423 CoolingDown (docs/02:424, docs/14:225) | Codes are `SESSION_COOLING_DOWN`/`SESSION_RESET_REQUIRED`; 404/409/401 variants exist |
| M5 | `StartRunHandler(IRunLauncher, IRunStore, ComplianceConsent)` (docs/02:270) | Five ctor deps (adds profile repo + settings catalog) |
| M6 | Branded engine/channel names need Chromium (docs/02:266) | Only `Bundled` channel accepted; Chrome/Edge folded by migration |
| M7 | `JobLens:Logging`, `joblens-*` log files (docs/15:71,93,104,253) | `JobScrape:Logging`, `jobscrape-api-*.jsonl` |
| M8 | `Browser:MaxHtmlBytes/MaxScreenshotBytes/MaxTraceBytes` + `Truncated` (docs/15:152-156,640) | No such options; HTML capped at 2,000,000 chars; no trace recorded |
| M9 | `IDiskSpace` port, handlers never touch `DriveInfo` (docs/15:678,729) | No such type; `PhysicalFileSystem.cs:53` calls `DriveInfo` |
| M10 | `MinimumFreeDiskBytes` unenforced (docs/02:1005) | `RunCoordinator.cs:86-89` enforces it (`STORAGE_LOW_DISK`) |
| M11 | 42 pinned versions, 6 configs/repos, 5 migrations (docs/02:289,307) | 41 `PackageVersion`, 10 configs, 10 repos, 3 migrations |
| M12 | `IExportWriter`, `BrowserOptions.DefaultEngine` Range(1,300) (docs/16:68,41) | Port is `IPostingExportWriter`; option is `NavigationTimeoutSeconds` Range(5,120) |
| M13 | ADR versions (EF 10.0.14, Sqlite 10.0.14, xunit 4.0.2, 4 test projects) + index statuses (docs/10-adr) | Props pin 11.0.0/4.0.3/18.12.0; six test projects; index disagrees with 0001/0006/0019 headers |
| M14 | CLI project, `Cli` composition root (ADRs 0008/0004, code comments) | No CLI project; `Program.cs` is the only host |
| M15 | `RunCoordinator` path `JobScrape.Api/Runs/`; `SearchPageResult` 4 fields (docs/02:426,609) | Under `Infrastructure/Runtime/`; adds `LastPage` |
| M16 | Postings doc omits the `LastSeenAt >= StartedAt` clause (docs/02:270, after id-set fix) | `RunReadStore.cs:159` requires it on every run |
| M17 | `CallerContext.LocalOperator` everywhere, two composition roots (docs/14:588,596) | Per-call MCP permission from bearer token; `/mcp` requires authorization |
| M18 | Runbook paths `joblens.db`, `run.json`/`page-001.html`/`trace.zip`, `BytesFreed` metrics (docs/09:379,385,411) | `jobscrape.db`, `assessment.json`/`screenshot.png`/`page.html`, `LogSwept(postings, directories)` |
| M19 | Dockerfile `sdk:10.0`, runbook demands 11.0.100 (docs/09:181 vs :80) | TFM `net11.0`, SDK `11.0.100` |
| M20 | Package.json mirror, quotas table, endpoint table, bunfig `[test]`, login notes (docs/12 §8.1,708,681,405; login/users pages) | Name/scripts/URLs differ; quotas edit limit+window; users/roles/quotas/auth/db endpoints omitted; no `[test]` section; browser calls `/bff/session*` |
| M21 | Migrated-from `04` tree missing `JobScrape.Mcp`, controller list of 5 (docs/04 §1-2) | slnx + csproj + props pin `ModelContextProtocol 2.4.0` |

## UNKNOWN direction (needs a human call)

Test-count totals (README 1,515/233 vs docs/04 1,375/47; grep finds 1,123 backend methods + 225 frontend calls, theories unexpandable statically); `dotnet run --project` vs `dotnet test --project` for MTP; XPlat vs MTP coverage flags; Polly/`new HttpClient` (docs demand factory+Polly, code news clients, no resilience package); `CancellationToken.None` sites (documented as never, code does it deliberately); team-permissions scope; JobLens rename status; D7 dashboard decision; challenge/429 timers; aspirational metrics table; security headers, `/readyz`, proxy sketch, partial endpoint table, CI workflow, pstack command counts.

## Verified consistent (spot-checked, no action)

xunit 4.0.3/adapter 4.0.1/TestSdk, Playwright 1.64.1, Dapper 2.1.89, ClosedXML 0.105.1, Serilog.AspNetCore 10.0.0, all `appsettings.json` defaults (paths, caps 3/60/60, retention 30/90/180, retry 2, quotas, feature flags), `ApiOnly: true`, runbook ports/polling/409 codes, cockpit poll/probe intervals, page sizes, routes for overview/runs, theme tokens, tsconfig bytes, cookie names, commit-hook rules vs `git log`, MCP test count 56, 13 e2e `test(` calls.

## Could not verify (read-only bounds, no runs)

Executed test totals; Playwright browser revisions (no browsers.json found); live payloads/DB schema; session/navigation runtime behavior; pstack mirror counts; OpenCode runtime semantics.

## Suggested fix order for the next session

1. Stack numbers (README table, docs/04 matrix, ADR versions, Dockerfile) — one grep-driven pass.
2. Status lines claiming no implementation (docs/README:5, 00:19,247).
3. Controller/handler/endpoint counts (docs/02, docs/04 §2).
4. Pattern docs (docs/14 ErrorKind/codes/validation, docs/15 logging/options/Polly stance).
5. Runbook paths/artifacts/commands (docs/09, docs/07 launch profile).
6. Cockpit doc routes/auth/env/ports (docs/12) + frontend README follow-ups.
7. ADRs index statuses + stale CLI references.
