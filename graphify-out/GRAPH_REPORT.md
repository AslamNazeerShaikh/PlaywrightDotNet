# Graph Report - PlaywrightDotNet  (2026-11-01)

## Corpus Check
- 293 files · ~138,782 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 2866 nodes · 6133 edges · 175 communities (145 shown, 30 thin omitted)
- Extraction: 91% EXTRACTED · 9% INFERRED · 0% AMBIGUOUS · INFERRED: 527 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `60f16a42`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- overview.tsx
- JobScrape.Api.Tests
- 2. Structured logging with Serilog
- package.json
- Common adapter blueprint
- docs/README.md
- Application Patterns
- JobLens
- .Ok
- Solution Layout (JobLens.slnx)
- compilerOptions
- index.ts
- IJobSource
- Web cockpit (v2 control plane)
- SearchProfileResponse
- .GetAsync
- Playwright for .NET
- ErrorMapperTests
- 2. Library-specific guidance
- github
- ErrorKind
- Conventional Commits
- ScrapeHarness
- RunStats
- Non-negotiables
- CLI-first (System.CommandLine + Hosting + Serilog)
- .HandleAsync
- Per-source circuit breaker
- RunScrapeHandler
- Cooldown Ladder
- LinkedIn consent gate
- http
- JobScrape Cockpit control plane
- PolicyTests
- Playwright for .NET
- Phase 0 open decisions (D1-D12 with recommended defaults)
- ADR-0011: Explicit Use-Case Handlers, No CQRS or MediatR
- ADR-0012: Serilog and Polly Runtime Hardening
- ADR-0013: AOT-Friendly Code, No NativeAOT Release
- ADR-0015: JobScrape solution naming, backend-first scaffold, native MTP tests
- JobScrape.slnx
- SearchProfileTests
- JobSnapshot
- PersistenceTests
- .Create
- ScrapeRunTests
- Bun BFF runtime (Bun.serve, /api/v1 proxy, SSE proxy, static hosting)
- CreateSearchProfileHandlerTests
- Result
- Json
- app.tsx
- JobPosting
- SalaryRange
- JobPostingId
- Architecture Document
- Unit of work per page batch
- Retry policy (max 2 attempts with backoff)
- Central Package Management
- Definition of Ready / Done
- SearchCriteriaTests
- Application patterns rule
- graphify knowledge graph
- Four Detection Layers
- Browser profile treated as a credential
- Result pattern and RFC 9457 Problem Details mapping
- Specification pattern for query criteria
- MoSCoW scope
- No live scraping in CI
- RFC 9457 Problem Details HTTP error contract
- commit-msg
- Pure hash router
- PostingTextValueTests
- JobScrape.Domain.Common
- AngleSharp
- ClosedXML
- Pre-Phase Spikes S1-S7
- System.CommandLine
- TimeProvider injection
- Gameplan Document
- Testing Strategy Document
- MTP CodeCoverage extension
- Async cancellation and bounded resource disposal
- Serilog structured logging with redaction
- AggregateRoot
- jobscrape_api_controllers
- ContentHash
- Cockpit Favicon Icon
- .OnModelCreating
- ScrapeRun
- BrowserProfile
- SourceQuota
- RunLimitsTests
- CapturingLogger
- ADR index
- GuardrailTests
- DomainConverters
- .Main
- RunStage
- app.test.tsx
- SourceCircuitBreaker
- DataQualityIssues
- JobScrape.Application.Results
- ADR-0014: Use Libraries as Designed; AOT Is Not a Constraint
- ErrorInfo
- JobScrape.Application.Features.Scraping
- DomainExceptionTests.cs
- graphify.js
- xunit
- SearchPageParser
- InitialCreate
- JobPostingStatus
- StorageOptions
- JobScrapeDbContext
- jobscrape_api
- jobscrape_application_features_health
- .EnrichAsync
- debug.ts
- RunScrapeHandler
- ScriptedSource
- .GetAsync
- .HandleAsync
- RunLimits
- SearchCriteria
- .Collapse
- CapturingLogger
- JobScrape.Infrastructure.Configuration
- SourceKey
- TestDatabase
- RunScope
- ApiFactory
- SearchProfile
- PageFingerprint
- JobScrape.Api.Tests
- SourceRunContext
- IUnitOfWork
- JobPostingRepository
- JobScrape.Architecture.Tests
- PageStats
- JobScrape.Application.Tests
- .HandleAsync
- .MigrateInDevelopment
- BrowserProfileId
- .TwoRunsPersistDedupeAndChargeTheDailyQuota
- JobScrape.Infrastructure.Tests
- WorkMode
- Compliance-first low-signal automation policy
- JobLocation
- SearchProfileRepository
- JobScrape.Domain.Tests
- ChallengeAction
- JobScrape.Infrastructure
- InMemoryStore
- .AddJobScrapeOptions
- App
- profiles.tsx
- sources.tsx
- .ValidationFailed
- SqlitePragmaInterceptor
- JobPosting
- OptionsTests
- .TimeProviderDelayHonorsCancellationAndRejectsNegative
- RecordingDelay
- system_collections_concurrent
- JobScrape.Api
- ISourceQuotaRepository
- RunStatsTests
- SourceOptions
- ScrapeRunRepository
- SearchProfileId
- IScrapeRunRepository
- ComplianceDecision.cs
- LoggingOptions
- .Extensions
- .AddJobScrapePersistence
- SystemRandomSource

## God Nodes (most connected - your core abstractions)
1. `ScrapeHarness` - 97 edges
2. `JobScrape.Domain.Common` - 85 edges
3. `JobScrape.Domain.Sourcing` - 77 edges
4. `JobPosting` - 66 edges
5. `SourceKey` - 62 edges
6. `JobScrape.Domain.Access` - 56 edges
7. `ScrapeRun` - 56 edges
8. `DomainException` - 51 edges
9. `RunScrapeHandlerTests` - 45 edges
10. `RunScrapeHandler` - 41 edges

## Surprising Connections (you probably didn't know these)
- `UI design in src/frontend` --conceptually_related_to--> `JobScrape Cockpit control plane`  [INFERRED]
  AGENTS.md → src/frontend/README.md
- `Session lifecycle state machine` --semantically_similar_to--> `BrowserProfile`  [INFERRED] [semantically similar]
  docs/07-anti-bot-and-compliance.md → docs/03-domain-model.md
- `JobScrape Cockpit control plane` --cites--> `ui-skills catalog router`  [EXTRACTED]
  src/frontend/README.md → AGENTS.md
- `JobLens` --conceptually_related_to--> `Clean Architecture dependency rule`  [INFERRED]
  README.md → docs/02-architecture.md
- `Code Conventions and csharpier workflow` --semantically_similar_to--> `XML documentation global build rule`  [INFERRED] [semantically similar]
  docs/04-solution-and-tooling.md → docs/10-adr/0017-xml-documentation-rule.md

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **ADR index covering ADR-0001 through ADR-0010** — docs_10_adr_readme_architecture_decision_record, docs_10_adr_0001_dotnet_10_lts_dotnet_10_lts, docs_10_adr_0002_xunit_v3_xunit_net_v3, docs_10_adr_0003_playwright_dotnet_playwright_for_dotnet, docs_10_adr_0004_modular_monolith_clean_architecture_modular_monolith, docs_10_adr_0005_sqlite_ef_core_sqlite, docs_10_adr_0006_browser_first_optional_api_fast_path_browser_first, docs_10_adr_0007_compliance_first_low_signal_automation_compliance_first_policy, docs_10_adr_0008_cli_first_api_later_cli_first, docs_10_adr_0009_test_pyramid_coverage_gates_test_pyramid, docs_10_adr_0010_react_bun_cockpit_react_bun_cockpit [EXTRACTED 1.00]
- **Challenge detection to stop-on-wall response flow** — docs_02_architecture_challengedetector, docs_07_anti_bot_and_compliance_challengeassessment, docs_02_architecture_artifactstore, docs_07_anti_bot_and_compliance_circuit_breaker, docs_07_anti_bot_and_compliance_cooldown_policy [EXTRACTED 1.00]
- **Compliance gating stack for risky sources** — docs_07_anti_bot_and_compliance_source_risk_tiers, docs_07_anti_bot_and_compliance_compliancepolicy, docs_08_site_playbooks_linkedin_consent_gate, docs_07_anti_bot_and_compliance_fair_use_checklist [EXTRACTED 1.00]
- **Result to edge error mapping flow** — docs_14_application_patterns_result_pattern, docs_14_application_patterns_problem_details, docs_02_architecture_result_and_problem_details, docs_15_runtime_hardening_global_exception_handling [EXTRACTED 1.00]
- **Scrape run orchestration flow** — docs_02_architecture_runscrapehandler, docs_02_architecture_ijobsource, docs_02_architecture_challenge_detector [EXTRACTED 1.00]
- **Phase 0 recommended default decision set (D1-D12)** — docs_11_open_decisions_joblens_project_name, docs_11_open_decisions_linkedin_opt_in, docs_11_open_decisions_indeed_opt_in, docs_11_open_decisions_full_posting_fields, docs_11_open_decisions_csv_excel_output, docs_11_open_decisions_os_scheduler, docs_11_open_decisions_normal_network_egress, docs_11_open_decisions_bundled_chromium_default, docs_11_open_decisions_recruiter_emails_out_of_scope, docs_11_open_decisions_data_retention_policy [EXTRACTED 1.00]
- **Per-source adapter extraction pipeline** — docs_08_site_playbooks_urlbuilder, docs_08_site_playbooks_searchpageparser, docs_08_site_playbooks_normalizer, docs_08_site_playbooks_challengeclassifier, docs_02_architecture_searchpageresult, docs_08_site_playbooks_contract_tests [EXTRACTED 1.00]
- **Cockpit control-plane request and live-update flow** — docs_12_web_cockpit_cockpit, docs_12_web_cockpit_dotnet_api_command_surface, docs_10_adr_0010_react_bun_cockpit_bun_bff, docs_12_web_cockpit_sse_event_stream, docs_12_web_cockpit_auth_csrf_model, docs_12_web_cockpit_tanstack_query_state [INFERRED 0.95]
- **Ops deck request flow** —  [INFERRED]
- **Deterministic C# formatting workflow** — docs_04_solution_and_tooling_code_conventions, docs_10_adr_0016_csharp_style_csharpier_style_guides, docs_10_adr_0016_csharp_style_csharpier_csharpier_formatting [INFERRED]
- **xml docs enforcement flow** — docs_04_solution_and_tooling_code_conventions, docs_10_adr_0017_xml_documentation_rule_xml_doc_build_rule, docs_10_adr_readme_adr_index [INFERRED]
- **** — docs_12_web_cockpit_control_plane, docs_12_web_cockpit_bun_bff, docs_12_web_cockpit_dotnet_data_plane [INFERRED]

## Communities (175 total, 30 thin omitted)

### Community 0 - "overview.tsx"
Cohesion: 0.21
Nodes (22): Abbr(), Cross(), Diamond(), Dot(), Glossary(), GlossaryTerm, Legend(), LegendItem (+14 more)

### Community 1 - "JobScrape.Api.Tests"
Cohesion: 0.17
Nodes (9): JobScrape.Api.Tests, JobScrape.Api.Http, JobScrape.Api.Controllers, microsoft_aspnetcore_http, microsoft_aspnetcore_mvc, microsoft_aspnetcore_mvc_testing, system_diagnostics, system_net (+1 more)

### Community 2 - "2. Structured logging with Serilog"
Cohesion: 0.04
Nodes (45): 1. Baseline decisions, 2.1 Layering, 2.2 Pipeline, 2.3 Bootstrap, 2.4 Enrichers, 2.5 Correlation and scopes, 2.6 Message templates and performance, 2.7 Redaction (+37 more)

### Community 3 - "package.json"
Cohesion: 0.05
Nodes (42): happy-dom, react-dom, tailwindcss, @tailwindcss/cli, @types/bun, @types/react, @types/react-dom, typescript (+34 more)

### Community 4 - "Common adapter blueprint"
Cohesion: 0.08
Nodes (28): Naukri Private Search API (jobapi/v3/search), JobSourceContractTests, Challenge kinds taxonomy, ChallengeAssessment (kind, evidence, confidence), ChallengeDetector (anti-bot section 9.3), Acceptance matrix, ChallengeClassifier, Common adapter blueprint (+20 more)

### Community 6 - "Application Patterns"
Cohesion: 0.05
Nodes (39): 1. Pattern map, 2.1 Decision, 2.2 Banned, 2.3 Replacements, 2.4 Why, 2.5 Allowed read/write separation, 2.6 Enforcement, 2. No CQRS, no MediatR (+31 more)

### Community 7 - "JobLens"
Cohesion: 0.05
Nodes (42): SQLite + EF Core 10, ADR-0010 React + Bun cockpit, Modular Monolith with Clean Architecture and inward dependency rule, v2 Web Cockpit, Decision gates G0–G5, First 15 commits, Phase 0 — Foundation, Phase 1 — Domain + Application (+34 more)

### Community 8 - ".Ok"
Cohesion: 0.15
Nodes (11): Result, Error, IsFailure, IsSuccess, Fact, InvalidOperationException, ResultOfTTests, ArgumentNullException (+3 more)

### Community 9 - "Solution Layout (JobLens.slnx)"
Cohesion: 0.11
Nodes (22): v1 Scope Boundary, v1 Success Criteria, NetArchTest architecture tests, ChallengeDetector and ChallengeAssessment, Clean Architecture dependency rule, JobLens.Application, JobLens CLI (composition root), JobLens.Domain (+14 more)

### Community 10 - "compilerOptions"
Cohesion: 0.09
Nodes (21): compilerOptions, allowImportingTsExtensions, allowJs, isolatedModules, jsx, lib, module, moduleDetection (+13 more)

### Community 11 - "index.ts"
Cohesion: 0.15
Nodes (13): RFC-9110, src_frontend_src_client_index, CockpitConfig, parseApiUrl(), parseConfig(), parsePort(), apiProxy(), createHandler() (+5 more)

### Community 12 - "IJobSource"
Cohesion: 0.10
Nodes (26): Akamai Bot Manager, Cloudflare Bot Management, ChallengeAssessment, ChallengeDetector, ChallengeKind, Error Taxonomy and Retry Rules, Extraction Priority (JSON-LD first), IJobSource (+18 more)

### Community 13 - "Web cockpit (v2 control plane)"
Cohesion: 0.19
Nodes (18): No Node.js policy, React + Bun web cockpit (v2.0), shadcn/ui copy-in components, HTTP/JSON commands + Server-Sent Events transport, Backend-first scaffold, Explicit Main + block namespaces, Architecture Decision Record (ADR), Bun native package manager (bun install/bun ci, bun.lock, minimumReleaseAge) (+10 more)

### Community 14 - "SearchProfileResponse"
Cohesion: 0.12
Nodes (16): CancellationToken, Guid, IReadOnlyList, Task, DateTimeOffset, Guid, IReadOnlyList, SearchProfileResponse (+8 more)

### Community 15 - ".GetAsync"
Cohesion: 0.06
Nodes (28): HealthPayload, IClassFixture, OptionsValidationException, ActionResult, CancellationToken, HttpGet, Task, HealthController (+20 more)

### Community 16 - "Playwright for .NET"
Cohesion: 0.22
Nodes (13): Headed Persistent Browser Profile, Playwright for .NET, Public Browser Diagnostics, WebKit Is Not Safari, xUnit.net v3, BrowserSessionFactory, ISessionManager, BrowserProfile (+5 more)

### Community 17 - "ErrorMapperTests"
Cohesion: 0.30
Nodes (7): ArgumentNullException, Fact, InlineData, IResult, Task, Theory, ErrorMapperTests

### Community 18 - "2. Library-specific guidance"
Cohesion: 0.14
Nodes (14): 1. Rules, 2. Library-specific guidance, 3. What we still enforce, 4. Reviewing a new library, 5. Related decisions, 6. References, ClosedXML, EF Core and Playwright (+6 more)

### Community 19 - "github"
Cohesion: 0.14
Nodes (13): headers, oauth, type, url, Authorization, mcp, servers, $schema (+5 more)

### Community 20 - "ErrorKind"
Cohesion: 0.10
Nodes (20): ErrorKind, Challenge, Conflict, CoolingDown, Forbidden, NotFound, QuotaExceeded, RateLimited (+12 more)

### Community 21 - "Conventional Commits"
Cohesion: 0.15
Nodes (13): 1.1 Header, 1.2 Body, 1.3 Footers, 1.4 Breaking changes, 1. Format, 2.1 Scopes, 2. Types used in this repository, 3.1 Examples (+5 more)

### Community 22 - "ScrapeHarness"
Cohesion: 0.06
Nodes (51): CapturingLogger, FakeSessions, RecordingArtifacts, RecordingDelay, JobDetailResult, DateTimeOffset, Guid, ScrapeRunSummary (+43 more)

### Community 23 - "RunStats"
Cohesion: 0.07
Nodes (24): ScrapeRunId, Value, DateTimeOffset, RunCancelled, DateTimeOffset, RunCompleted, DateTimeOffset, RunFailed (+16 more)

### Community 24 - "Non-negotiables"
Cohesion: 0.16
Nodes (15): Anti-ban by being a good citizen, Browser hygiene standard, Detection surface matrix, Hard exclusions (no CAPTCHA solving, no spoofing), Inert stealth init script finding, Low-signal automation policy, No fingerprint spoofing policy, Profile isolation per source (+7 more)

### Community 25 - "CLI-first (System.CommandLine + Hosting + Serilog)"
Cohesion: 0.18
Nodes (11): .NET 10 LTS, global.json SDK pin (10.0.403, rollForward latestFeature), Clean Architecture layers (Domain, Application, Infrastructure, Cli), Focused (tactical) DDD for Sourcing and Access, Modular monolith, EF Core 10.0.14, SQLite as source of truth, WAL single-writer configuration (+3 more)

### Community 26 - ".HandleAsync"
Cohesion: 0.19
Nodes (13): CancellationToken, Guid, Task, GetSearchProfileHandler, ArgumentNullException, CancellationToken, Fact, Guid (+5 more)

### Community 27 - "Per-source circuit breaker"
Cohesion: 0.22
Nodes (10): CircuitBreaker (per source), Exponential backoff policy, Challenge and block metrics, Per-source circuit breaker, Cooldown policy, doctor command, Incident runbook, Review cadence (+2 more)

### Community 28 - "RunScrapeHandler"
Cohesion: 0.16
Nodes (15): ArtifactStore, CompliancePolicy, RunScrapeHandler, TimeProvider and IRandomSource Injection, JobLens Bounded Contexts, Per-Source Circuit Breaker, PolitenessPolicy, QuotaPolicy (+7 more)

### Community 29 - "Cooldown Ladder"
Cohesion: 0.18
Nodes (12): JobLens CLI Surface, joblens doctor, CLI Exit Codes, Cooldown Ladder, Data minimization and privacy, Anti-Bot Metrics and Alerts, Retention Policy, OS Scheduling (cron/launchd/Task Scheduler) (+4 more)

### Community 30 - "LinkedIn consent gate"
Cohesion: 0.27
Nodes (10): CompliancePolicy, ComplianceTier, CompliancePolicy gate, Fair use checklist, HumanPacing, Pacing and quotas, robots.txt findings (2026-10-29), Source risk tiers and gates (+2 more)

### Community 31 - "http"
Cohesion: 0.20
Nodes (9): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, profiles, http (+1 more)

### Community 32 - "JobScrape Cockpit control plane"
Cohesion: 0.25
Nodes (9): UI design in src/frontend, ui-skills catalog router, BFF API proxy, .NET data plane API localhost 5207, Bun React TypeScript Tailwind stack, JobScrape Cockpit control plane, globals.css stylesheet link, main.tsx module entry (+1 more)

### Community 33 - "PolicyTests"
Cohesion: 0.16
Nodes (12): CompliancePolicy, TimeSpan, RetryPolicy, ArgumentNullException, DomainException, Fact, InlineData, Theory (+4 more)

### Community 34 - "Playwright for .NET"
Cohesion: 0.20
Nodes (11): xUnit.net v3, Playwright for .NET, Manual/nightly live canaries (never in CI), Local fixture server for Playwright E2E, Risk-based coverage gates (Domain 95/90, Application 90/85, overall 85/80), Test pyramid, FakeTimeProvider namespace rename, JobScrape naming with backend-first MTP tests (+3 more)

### Community 35 - "Phase 0 open decisions (D1-D12 with recommended defaults)"
Cohesion: 0.31
Nodes (9): Per-source compliance tiers and consent gates, CSV + Excel export (JSON optional, Postgres later), Data retention 30/90/180 days (D12), Full posting field set (D4), Indeed opt-in with conservative caps (D3), JobLens working project name (D1), LinkedIn opt-in high-risk, default disabled (D2), Phase 0 open decisions (D1-D12 with recommended defaults) (+1 more)

### Community 36 - "ADR-0011: Explicit Use-Case Handlers, No CQRS or MediatR"
Cohesion: 0.22
Nodes (9): ADR-0011: Explicit Use-Case Handlers, No CQRS or MediatR, Alternatives considered, Consequences, Context, Decision, Follow-ups, Negative / trade-offs, Positive (+1 more)

### Community 37 - "ADR-0012: Serilog and Polly Runtime Hardening"
Cohesion: 0.22
Nodes (9): ADR-0012: Serilog and Polly Runtime Hardening, Alternatives considered, Consequences, Context, Decision, Follow-ups, Negative / trade-offs, Positive (+1 more)

### Community 38 - "ADR-0013: AOT-Friendly Code, No NativeAOT Release"
Cohesion: 0.22
Nodes (9): ADR-0013: AOT-Friendly Code, No NativeAOT Release, Alternatives considered, Consequences, Context, Decision, Follow-ups, Negative / trade-offs, Positive (+1 more)

### Community 39 - "ADR-0015: JobScrape solution naming, backend-first scaffold, native MTP tests"
Cohesion: 0.22
Nodes (9): ADR-0015: JobScrape solution naming, backend-first scaffold, native MTP tests, Alternatives considered, Consequences, Context, Decision, Follow-ups, Negative / trade-offs, Positive (+1 more)

### Community 40 - "JobScrape.slnx"
Cohesion: 0.29
Nodes (7): JobScrape.Application, net10.0, Microsoft.Extensions.Logging.Abstractions, Microsoft.NET.Sdk, JobScrape.Domain, net10.0, Microsoft.NET.Sdk

### Community 41 - "SearchProfileTests"
Cohesion: 0.20
Nodes (9): DateTimeOffset, SearchProfileUpdated, ArgumentNullException, DateTimeOffset, DomainException, Fact, InlineData, Theory (+1 more)

### Community 42 - "JobSnapshot"
Cohesion: 0.08
Nodes (31): DateTimeOffset, IReadOnlyList, JobSnapshot, ApplyUrl, Company, CompanyUrl, Description, ExperienceMaxYears (+23 more)

### Community 43 - "PersistenceTests"
Cohesion: 0.20
Nodes (10): DbUpdateException, IMigrator, CancellationToken, DateTimeOffset, Fact, Task, PersistenceTests, Ct (+2 more)

### Community 44 - ".Create"
Cohesion: 0.07
Nodes (21): CanonicalUrl, Value, DomainException, Fact, InlineData, Theory, SourceKeyTests, ArgumentNullException (+13 more)

### Community 45 - "ScrapeRunTests"
Cohesion: 0.17
Nodes (7): ArgumentNullException, DateTimeOffset, DomainException, Fact, InlineData, Theory, ScrapeRunTests

### Community 46 - "Bun BFF runtime (Bun.serve, /api/v1 proxy, SSE proxy, static hosting)"
Cohesion: 0.29
Nodes (7): Same-origin Bun BFF, Cockpit auth: loopback bind, HttpOnly cookie, CSRF double-submit, BFF bearer token, Bun BFF runtime (Bun.serve, /api/v1 proxy, SSE proxy, static hosting), Web Cockpit Control Plane, CSP and security headers, .NET API Data Plane, SSE Run Event Stream

### Community 47 - "CreateSearchProfileHandlerTests"
Cohesion: 0.20
Nodes (13): Handler, ArgumentNullException, DateTimeOffset, Fact, InlineData, InMemoryStore, IReadOnlyDictionary, Result (+5 more)

### Community 48 - "Result"
Cohesion: 0.33
Nodes (5): Result, Error, IsFailure, IsSuccess, Value

### Community 49 - "Json"
Cohesion: 0.09
Nodes (18): CriteriaJson, JsonException, JsonSerializerOptions, LimitsJson, SalaryJson, SalaryPeriod, Day, Hour (+10 more)

### Community 50 - "app.tsx"
Cohesion: 0.15
Nodes (20): RFC-9457, readHash(), RulerTabs(), ApiClient, ApiProblem, ApiResult, CreateProfileInput, get() (+12 more)

### Community 51 - "JobPosting"
Cohesion: 0.06
Nodes (29): CancellationToken, Task, IReadOnlyList, JobPosting, ApplyUrl, Company, CompanyUrl, ContentHash (+21 more)

### Community 52 - "SalaryRange"
Cohesion: 0.20
Nodes (10): SalaryRange, Currency, Max, Min, Period, DomainException, Fact, InlineData (+2 more)

### Community 53 - "JobPostingId"
Cohesion: 0.13
Nodes (10): Guid, Guid, JobPostingId, Value, Guid, Guid, DomainException, Fact (+2 more)

### Community 54 - "Architecture Document"
Cohesion: 1.00
Nodes (3): Architecture Document, Application Patterns Document, Runtime Hardening Document

### Community 55 - "Unit of work per page batch"
Cohesion: 0.67
Nodes (3): IUnitOfWork, SQLite as source of truth, Unit of work per page batch

### Community 56 - "Retry policy (max 2 attempts with backoff)"
Cohesion: 0.67
Nodes (3): Never retry 403/429/CAPTCHA/challenge responses, PageFetcher, Retry policy (max 2 attempts with backoff)

### Community 57 - "Central Package Management"
Cohesion: 0.67
Nodes (3): Central Package Management, Layered projects and dependency graph, global.json SDK Pin

### Community 58 - "Definition of Ready / Done"
Cohesion: 0.67
Nodes (3): Backlog epics E0–E9, Definition of Ready / Done, Coverage and quality metrics

### Community 59 - "SearchCriteriaTests"
Cohesion: 0.23
Nodes (6): ArgumentNullException, DomainException, Fact, InlineData, Theory, SearchCriteriaTests

### Community 71 - "PostingTextValueTests"
Cohesion: 0.21
Nodes (5): DomainException, Fact, InlineData, Theory, PostingTextValueTests

### Community 72 - "JobScrape.Domain.Common"
Cohesion: 0.10
Nodes (7): JobScrape.Domain.Sourcing, JobScrape.Application.Abstractions, JobScrape.Infrastructure.Persistence.Repositories, JobScrape.Domain.Common, JobScrape.Domain.Abstractions, JobScrape.Domain.Access, JobScrape.Domain

### Community 83 - "AggregateRoot"
Cohesion: 0.17
Nodes (13): IList, IReadOnlyList, List, AggregateRoot, DomainEvents, DateTimeOffset, DomainEvent, ArgumentNullException (+5 more)

### Community 85 - "ContentHash"
Cohesion: 0.14
Nodes (11): IEnumerable, ContentHash, Value, DateTimeOffset, JobPostingUpdated, ArgumentNullException, DomainException, Fact (+3 more)

### Community 87 - ".OnModelCreating"
Cohesion: 0.13
Nodes (11): JobScrape.Infrastructure.Persistence.Configurations, IEntityTypeConfiguration, EntityTypeBuilder, BrowserProfileConfiguration, EntityTypeBuilder, JobPostingConfiguration, ScrapeRunConfiguration, SearchProfileConfiguration (+3 more)

### Community 88 - "ScrapeRun"
Cohesion: 0.12
Nodes (17): Exception, DomainException, DateTimeOffset, ScrapeRun, CreatedAt, FailureReason, FinishedAt, Id (+9 more)

### Community 89 - "BrowserProfile"
Cohesion: 0.05
Nodes (39): BrowserChannel, Bundled, Chrome, Edge, BrowserEngine, Chromium, Firefox, WebKit (+31 more)

### Community 90 - "SourceQuota"
Cohesion: 0.08
Nodes (26): QuotaDecision, QuotaKind, Details, Pages, Runs, DateOnly, DateTimeOffset, SourceQuota (+18 more)

### Community 91 - "RunLimitsTests"
Cohesion: 0.33
Nodes (5): DomainException, Fact, InlineData, Theory, RunLimitsTests

### Community 92 - "CapturingLogger"
Cohesion: 0.06
Nodes (33): IExceptionHandler, JsonElement, Level, PathString, RequestDelegate, HttpContext, Task, CorrelationMiddleware (+25 more)

### Community 93 - "ADR index"
Cohesion: 0.23
Nodes (12): CI Outline restore build test E2E coverage, Code Conventions and csharpier workflow, Solution and Tooling, Options binding and startup validation, Solution and Tooling, csharpier formatting ownership, ADR-0016 C# style guides and csharpier formatting, C# style guides ktaranov plus Google (+4 more)

### Community 94 - "GuardrailTests"
Cohesion: 0.11
Nodes (14): Assembly, JobScrape.Architecture.Tests, MemberInfo, netarchtest_rules, Fact, InlineData, TestResult, Theory (+6 more)

### Community 95 - "DomainConverters"
Cohesion: 0.14
Nodes (14): CompanyName, Value, DateTimeOffset, ValidatedFields, JobSkill, Name, NormalizedName, JobTitle (+6 more)

### Community 96 - ".Main"
Cohesion: 0.12
Nodes (16): ControllerBase, HttpPost, ActionResult, CancellationToken, Guid, HttpGet, IResult, Task (+8 more)

### Community 97 - "RunStage"
Cohesion: 0.14
Nodes (12): RunStage, Cancelled, Completed, Enriching, Failed, PausedForChallenge, Pending, Persisting (+4 more)

### Community 98 - "app.test.tsx"
Cohesion: 0.15
Nodes (14): ref_bun_test, @happy-dom/global-registrator, @testing-library/react, Compass(), createApiClient(), FetchImpl, healthyHandler(), profilesBackend() (+6 more)

### Community 99 - "SourceCircuitBreaker"
Cohesion: 0.05
Nodes (40): ChallengeKind, AccessDenied, Captcha, LoginRequired, None, RateLimited, SoftChallenge, SourceChanged (+32 more)

### Community 100 - "DataQualityIssues"
Cohesion: 0.22
Nodes (8): DataQualityIssues, DescriptionMissing, MissingCompany, MissingLocation, None, ParseLowConfidence, PostedDateUnknown, SalaryUnknown

### Community 101 - "JobScrape.Application.Results"
Cohesion: 0.14
Nodes (6): JobScrape.Application.Results, JobScrape.Infrastructure.Persistence, JobScrape.Application.Tests, JobScrape.Infrastructure.Tests.Persistence, JobScrape.Application.Features.SearchProfiles, microsoft_extensions_dependencyinjection

### Community 102 - "ADR-0014: Use Libraries as Designed; AOT Is Not a Constraint"
Cohesion: 0.25
Nodes (8): ADR-0014: Use Libraries as Designed; AOT Is Not a Constraint, Alternatives considered, Consequences, Context, Decision, Negative / trade-offs, Positive, References

### Community 103 - "ErrorInfo"
Cohesion: 0.18
Nodes (9): IReadOnlyDictionary, ErrorInfo, DateTimeOffset, Guid, Errors, Fact, ErrorInfoTests, ArgumentNullException (+1 more)

### Community 104 - "JobScrape.Application.Features.Scraping"
Cohesion: 0.14
Nodes (3): JobScrape.Application.Features.Scraping, JobScrape.Application.Tests.Scraping, JobScrape.Application.Features.Sources

### Community 105 - "DomainExceptionTests.cs"
Cohesion: 0.40
Nodes (3): JobScrape.Domain.Tests, Fact, DomainExceptionTests

### Community 106 - "graphify.js"
Cohesion: 0.47
Nodes (5): hasGraph(), location, IMPORTANT: keep the reminder string free of backticks and $(...) constructs., server(), setup()

### Community 107 - "xunit"
Cohesion: 0.13
Nodes (5): JobScrape.Domain.Tests.Access, JobScrape.Domain.Tests.Abstractions, JobScrape.Domain.Tests.Common, JobScrape.Domain.Tests.Sourcing, xunit

### Community 108 - "SearchPageParser"
Cohesion: 0.40
Nodes (6): HtmlParser, JSON-LD extraction, Visibility guards, Extraction priority (JSON-LD first), Honeypot and visibility guards, SearchPageParser

### Community 109 - "InitialCreate"
Cohesion: 0.10
Nodes (14): JobScrape.Infrastructure.Persistence.Migrations, Migration, MigrationBuilder, ModelSnapshot, DateOnly, Guid, DateOnly, Guid (+6 more)

### Community 110 - "JobPostingStatus"
Cohesion: 0.40
Nodes (4): JobPostingStatus, Active, Expired, Unknown

### Community 111 - "StorageOptions"
Cohesion: 0.17
Nodes (9): RunOptions, RetryMaxAttempts, StorageOptions, ArtifactsRoot, DatabasePath, MinimumFreeDiskBytes, PostingRetentionDays, ProfilesRoot (+1 more)

### Community 112 - "JobScrapeDbContext"
Cohesion: 0.11
Nodes (16): DbContext, DbSet, IDesignTimeDbContextFactory, ModelConfigurationBuilder, DateTimeOffset, JobScrapeDbContext, BrowserProfiles, JobPostings (+8 more)

### Community 115 - ".EnrichAsync"
Cohesion: 0.17
Nodes (14): HashSet, RunScope, CancellationToken, Task, TimeSpan, CancellationToken, DateTimeOffset, Func (+6 more)

### Community 116 - "debug.ts"
Cohesion: 0.18
Nodes (12): react, cardinals(), gridLines(), TacMap(), renderDebugEnabled, formatMs(), latencyStats, LogEvent (+4 more)

### Community 117 - "RunScrapeHandler"
Cohesion: 0.34
Nodes (6): Exception, Guid, ILogger, LoggerMessage, TimeProvider, RunScrapeHandler

### Community 118 - "ScriptedSource"
Cohesion: 0.15
Nodes (11): Queue, ScriptedSource, JobReference, ScriptedSource, Capabilities, DetailRequests, Key, Requests (+3 more)

### Community 120 - ".HandleAsync"
Cohesion: 0.21
Nodes (9): CancellationToken, Task, CancellationToken, Dictionary, Task, TimeProvider, CreateSearchProfileHandler, IReadOnlyList (+1 more)

### Community 121 - "RunLimits"
Cohesion: 0.25
Nodes (7): TimeSpan, RunLimits, MaxDelay, MaxDetails, MaxJobs, MaxPages, MinDelay

### Community 122 - "SearchCriteria"
Cohesion: 0.22
Nodes (7): IReadOnlyList, SearchCriteria, ExperienceYears, Keywords, Locations, PostedWithinDays, RemoteOnly

### Community 123 - ".Collapse"
Cohesion: 0.23
Nodes (6): Text, ArgumentNullException, Fact, InlineData, Theory, TextTests

### Community 124 - "CapturingLogger"
Cohesion: 0.16
Nodes (11): ILogger, LogEntry, Dictionary, EventId, Exception, Func, IDisposable, LogLevel (+3 more)

### Community 125 - "JobScrape.Infrastructure.Configuration"
Cohesion: 0.13
Nodes (7): JobScrape.Api.Composition, JobScrape.Application.Features.Health, JobScrape.Infrastructure.Configuration, JobScrape.Api, JobScrape.Infrastructure.Runtime, JobScrape.Infrastructure.Tests.Configuration, microsoft_extensions_time_testing

### Community 126 - "SourceKey"
Cohesion: 0.08
Nodes (29): ArgumentException, IJobSource, Capabilities, Key, Dictionary, JobSourceRegistry, SourceCapabilities, TimeSpan (+21 more)

### Community 127 - "TestDatabase"
Cohesion: 0.18
Nodes (10): AsyncServiceScope, IAsyncDisposable, ServiceProvider, ValueTask, TestDatabase, ConnectionString, Path, TestScope (+2 more)

### Community 128 - "RunScope"
Cohesion: 0.17
Nodes (12): RunScope, Adapter, ArtifactDirectory, Browser, Circuit, Context, Enrich, HitWall (+4 more)

### Community 129 - "ApiFactory"
Cohesion: 0.17
Nodes (13): IWebHostBuilder, Program, ApiFactory, DatabasePath, DeleteLogsOnDispose, LogDirectory, ArgumentNullException, CancellationToken (+5 more)

### Community 130 - "SearchProfile"
Cohesion: 0.18
Nodes (11): DateTimeOffset, SearchProfile, CreatedAt, Criteria, Enabled, Id, Limits, Name (+3 more)

### Community 131 - "PageFingerprint"
Cohesion: 0.33
Nodes (6): PageFingerprint, FirstExternalId, ResultRange, DomainException, Fact, PageFingerprintTests

### Community 132 - "JobScrape.Api.Tests"
Cohesion: 0.20
Nodes (10): Microsoft.AspNetCore.Mvc.Testing, Serilog.Sinks.InMemory, JobScrape.Api.Tests, net10.0, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio (+2 more)

### Community 133 - "SourceRunContext"
Cohesion: 0.17
Nodes (9): CancellationToken, Task, SearchPageRequest, SourceRunContext, CancellationToken, Task, CancellationToken, Task (+1 more)

### Community 134 - "IUnitOfWork"
Cohesion: 0.22
Nodes (9): ISearchProfileRepository, IUnitOfWork, ArgumentNullException, CancellationToken, DateTimeOffset, Fact, Task, SearchProfileReadStoreTests (+1 more)

### Community 135 - "JobPostingRepository"
Cohesion: 0.47
Nodes (4): CancellationToken, JobScrapeDbContext, Task, JobPostingRepository

### Community 136 - "JobScrape.Architecture.Tests"
Cohesion: 0.22
Nodes (9): NetArchTest.Rules, JobScrape.Architecture.Tests, net10.0, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3 (+1 more)

### Community 137 - "PageStats"
Cohesion: 0.20
Nodes (9): PageStats, PostingsFound, PostingsNew, PostingsUpdated, DomainException, Fact, InlineData, Theory (+1 more)

### Community 138 - "JobScrape.Application.Tests"
Cohesion: 0.22
Nodes (9): JobScrape.Application.Tests, net10.0, coverlet.collector, Microsoft.Extensions.TimeProvider.Testing, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3 (+1 more)

### Community 139 - ".HandleAsync"
Cohesion: 0.06
Nodes (35): FakeTimeProvider, ActionResult, CancellationToken, HttpGet, Task, SourcesController, ArgumentNullException, CancellationToken (+27 more)

### Community 140 - ".MigrateInDevelopment"
Cohesion: 0.21
Nodes (8): IConfiguration, IServiceCollection, DatabaseRegistration, ArgumentNullException, Fact, Task, DatabaseRegistrationTests, WebApplication

### Community 141 - "BrowserProfileId"
Cohesion: 0.17
Nodes (7): CancellationToken, Task, CancellationToken, DateOnly, Task, BrowserProfileId, Value

### Community 142 - ".TwoRunsPersistDedupeAndChargeTheDailyQuota"
Cohesion: 0.10
Nodes (17): CancellationToken, Task, IArtifactStore, IDelay, CancellationToken, Task, ISessionManager, RunScrapeRequest (+9 more)

### Community 143 - "JobScrape.Infrastructure.Tests"
Cohesion: 0.22
Nodes (9): JobScrape.Infrastructure.Tests, net10.0, coverlet.collector, Microsoft.Extensions.TimeProvider.Testing, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3 (+1 more)

### Community 144 - "WorkMode"
Cohesion: 0.33
Nodes (5): WorkMode, Hybrid, Onsite, Remote, Unknown

### Community 145 - "Compliance-first low-signal automation policy"
Cohesion: 0.29
Nodes (7): Headed first contact + persistent contexts per source, Browser-first automation as source of truth, Optional Naukri API fast path (jobapi/v3/search with RSA nkparam), Compliance-first low-signal automation policy, Stop-on-wall with manual challenge solve, Normal network egress, no rotation (D8), Challenge handoff to host headed browser

### Community 146 - "JobLocation"
Cohesion: 0.22
Nodes (8): LocationJson, JobLocation, City, Country, Raw, Region, WorkMode, LocationJson

### Community 147 - "SearchProfileRepository"
Cohesion: 0.48
Nodes (4): CancellationToken, JobScrapeDbContext, Task, SearchProfileRepository

### Community 148 - "JobScrape.Domain.Tests"
Cohesion: 0.25
Nodes (8): JobScrape.Domain.Tests, net10.0, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3, Microsoft.NET.Sdk

### Community 149 - "ChallengeAction"
Cohesion: 0.13
Nodes (12): ChallengeAction, Continue, ManualPause, OpenCircuit, Pause, Retry, Stop, StopAndInspect (+4 more)

### Community 150 - "JobScrape.Infrastructure"
Cohesion: 0.29
Nodes (7): Microsoft.EntityFrameworkCore.Design, Microsoft.EntityFrameworkCore.Sqlite, Microsoft.Extensions.Options.DataAnnotations, JobScrape.Infrastructure, net10.0, Microsoft.Extensions.Logging.Abstractions, Microsoft.NET.Sdk

### Community 151 - "InMemoryStore"
Cohesion: 0.17
Nodes (11): InMemoryStore, BrowserProfiles, CancelAtSave, CancelOnSave, Circuits, FailAtSave, Postings, Quotas (+3 more)

### Community 152 - ".AddJobScrapeOptions"
Cohesion: 0.14
Nodes (14): IValidateOptions, IOptions, IServiceCollection, IValidateOptions, OptionsRegistration, ComplianceOptions, AcknowledgeSourceRisk, AllowHighRiskSources (+6 more)

### Community 153 - "App"
Cohesion: 0.38
Nodes (10): App(), checkHealth(), createProfile(), loadProfiles(), loadSources(), clockTime(), planeDown(), useNow() (+2 more)

### Community 154 - "profiles.tsx"
Cohesion: 0.25
Nodes (8): codeTone(), OverviewPage(), CodeRow(), orDash(), ProfilesPage(), submit(), readForm(), SOURCES

### Community 155 - "sources.tsx"
Cohesion: 0.29
Nodes (8): plural(), SourceStatus, circuitText(), circuitTone(), SourceCard(), SourcesPage(), tierTone(), closed

### Community 156 - ".ValidationFailed"
Cohesion: 0.33
Nodes (4): IReadOnlyDictionary, ArgumentNullException, Fact, ErrorsTests

### Community 157 - "SqlitePragmaInterceptor"
Cohesion: 0.28
Nodes (6): ConnectionEndEventData, DbConnection, DbConnectionInterceptor, CancellationToken, Task, SqlitePragmaInterceptor

### Community 158 - "JobPosting"
Cohesion: 0.31
Nodes (9): Idempotent upsert by (SourceKey, ExternalJobId), IJobPostingRepository, JobNormalizer, JobPosting aggregate, ContentHash, DeduplicationPolicy, JobPosting, JobSkill (+1 more)

### Community 159 - "OptionsTests"
Cohesion: 0.36
Nodes (4): Dictionary, Fact, ValidateOptionsResult, OptionsTests

### Community 160 - ".TimeProviderDelayHonorsCancellationAndRejectsNegative"
Cohesion: 0.19
Nodes (10): ArgumentOutOfRangeException, CancellationTokenSource, CancellationToken, Task, TimeProvider, TimeSpan, TimeProviderDelay, ArgumentNullException (+2 more)

### Community 161 - "RecordingDelay"
Cohesion: 0.25
Nodes (7): List, TimeSpan, RecordingArtifacts, Captured, RecordingDelay, CancelOnWait, Waits

### Community 163 - "JobScrape.Api"
Cohesion: 0.15
Nodes (13): Microsoft.AspNetCore.OpenApi, Serilog.AspNetCore, Serilog.Enrichers.AssemblyName, Serilog.Enrichers.Demystifier, Serilog.Enrichers.Environment, Serilog.Enrichers.Process, Serilog.Enrichers.Span, Serilog.Enrichers.Thread (+5 more)

### Community 164 - "ISourceQuotaRepository"
Cohesion: 0.47
Nodes (4): CancellationToken, DateOnly, Task, ISourceQuotaRepository

### Community 165 - "RunStatsTests"
Cohesion: 0.47
Nodes (3): ArgumentNullException, Fact, RunStatsTests

### Community 166 - "SourceOptions"
Cohesion: 0.15
Nodes (12): SourceOptions, BaseUrl, ChallengeCooldownMinutes, DailyDetailCap, DailyPageCap, DailyRunCap, Enabled, MaxDelayMs (+4 more)

### Community 167 - "ScrapeRunRepository"
Cohesion: 0.47
Nodes (4): CancellationToken, JobScrapeDbContext, Task, ScrapeRunRepository

### Community 168 - "SearchProfileId"
Cohesion: 0.19
Nodes (6): CancellationToken, Task, SearchProfileId, Value, DateTimeOffset, SearchProfileCreated

### Community 169 - "IScrapeRunRepository"
Cohesion: 0.60
Nodes (3): CancellationToken, Task, IScrapeRunRepository

### Community 172 - "LoggingOptions"
Cohesion: 0.20
Nodes (8): IConfiguration, IOptions, IServiceCollection, LoggingRegistration, LoggingOptions, Directory, FileSizeLimitBytes, RetentionDays

### Community 177 - ".Extensions"
Cohesion: 0.50
Nodes (3): Dictionary, IResult, ErrorMapper

### Community 181 - ".AddJobScrapePersistence"
Cohesion: 0.18
Nodes (12): IBrowserProfileRepository, IJobPostingRepository, CancellationToken, Task, ISourceCircuitBreakerRepository, ScrapeStores, ISearchProfileStore, IServiceCollection (+4 more)

## Knowledge Gaps
- **720 isolated node(s):** `$schema`, `type`, `url`, `oauth`, `Authorization` (+715 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1068 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **30 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `SourceKey` connect `SourceKey` to `SearchProfile`, `SourceRunContext`, `JobPostingRepository`, `.HandleAsync`, `BrowserProfileId`, `.TwoRunsPersistDedupeAndChargeTheDailyQuota`, `ScrapeHarness`, `RunStats`, `PolicyTests`, `ISourceQuotaRepository`, `SearchProfileId`, `SearchProfileTests`, `JobSnapshot`, `PersistenceTests`, `.Create`, `JobPosting`, `.AddJobScrapePersistence`, `ContentHash`, `ScrapeRun`, `BrowserProfile`, `SourceQuota`, `DomainConverters`, `SourceCircuitBreaker`, `ErrorInfo`, `.EnrichAsync`, `ScriptedSource`?**
  _High betweenness centrality (0.068) - this node is a cross-community bridge._
- **Why does `ScrapeHarness` connect `ScrapeHarness` to `RecordingDelay`, `RunLimits`, `SearchProfile`, `.HandleAsync`, `.TwoRunsPersistDedupeAndChargeTheDailyQuota`, `CreateSearchProfileHandlerTests`, `ScriptedSource`, `InMemoryStore`, `BrowserProfile`, `CapturingLogger`, `JobScrape.Infrastructure.Configuration`, `SourceKey`?**
  _High betweenness centrality (0.053) - this node is a cross-community bridge._
- **Why does `JobPosting` connect `JobPosting` to `RunScope`, `JobPostingRepository`, `BrowserProfileId`, `JobLocation`, `ScrapeHarness`, `InMemoryStore`, `JobSnapshot`, `.Create`, `SalaryRange`, `JobPostingId`, `JobScrape.Domain.Common`, `AggregateRoot`, `ContentHash`, `.OnModelCreating`, `ScrapeRun`, `DomainConverters`, `DataQualityIssues`, `JobPostingStatus`, `JobScrapeDbContext`, `.EnrichAsync`, `ScriptedSource`, `SourceKey`?**
  _High betweenness centrality (0.047) - this node is a cross-community bridge._
- **What connects `$schema`, `type`, `url` to the rest of the system?**
  _720 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `2. Structured logging with Serilog` be split into smaller, more focused modules?**
  _Cohesion score 0.044444444444444446 - nodes in this community are weakly interconnected._
- **Should `package.json` be split into smaller, more focused modules?**
  _Cohesion score 0.046511627906976744 - nodes in this community are weakly interconnected._
- **Should `Common adapter blueprint` be split into smaller, more focused modules?**
  _Cohesion score 0.08465608465608465 - nodes in this community are weakly interconnected._