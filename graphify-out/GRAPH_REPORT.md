# Graph Report - PlaywrightDotNet  (2026-11-02)

## Corpus Check
- 353 files · ~180,615 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 3644 nodes · 8400 edges · 218 communities (187 shown, 31 thin omitted)
- Extraction: 91% EXTRACTED · 9% INFERRED · 0% AMBIGUOUS · INFERRED: 719 edges (avg confidence: 0.84)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `81fd1584`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- useRenderLog
- RunsEndpointTests.cs
- 2. Structured logging with Serilog
- package.json
- Common adapter blueprint
- docs/README.md
- Application Patterns
- Walking skeleton first
- .Ok
- Solution Layout (JobLens.slnx)
- compilerOptions
- index.ts
- FakeSource
- Web cockpit (v2 control plane)
- SearchProfileResponse
- CapturingLogger
- Playwright for .NET
- ErrorInfo
- 2. Library-specific guidance
- github
- ErrorKind
- Conventional Commits
- ScrapeHarness
- ScrapeRunId
- Non-negotiables
- PageStats
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
- SearchProfile
- .Create
- .CreateAsync
- .Create
- ScrapeRunTests
- .HandleAsync
- CreateSearchProfileHandlerTests
- Result
- Json
- api.ts
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
- RuntimeHardeningTests
- Code Conventions and csharpier workflow
- GuardrailTests
- DomainConverters
- SearchProfilesController
- RunStage
- ref_bun_test
- SourceCircuitBreakerTests
- DataQualityIssues
- JobScrape.Application.Abstractions
- ADR-0014: Use Libraries as Designed; AOT Is Not a Constraint
- .OpenScopeAsync
- JobScrape.Domain.Access
- .Build
- graphify.js
- xunit
- SearchPageParser
- InitialCreate
- JobPostingStatus
- StorageOptions
- IJobSource
- jobscrape_api
- jobscrape_application_features_health
- RunScrapeHandler
- SessionHandlersTests
- ScriptedSource
- CapturingLogger
- BrowserProfileRepository
- .HandleAsync
- NaukriSearchParserTests
- IBrowserProfileRepository
- .CollapseOrNull
- FakeTimeProvider
- JobScrape.Infrastructure.Configuration
- SourceKey
- JobScrapeDbContext
- RunScope
- ApiFactory
- SessionsController
- CLI-first (System.CommandLine + Hosting + Serilog)
- JobScrape.Api.Tests
- BrowserTests
- .ListsByNameAndGetsByIdWithoutTracking
- ExternalJobId
- JobScrape.Architecture.Tests
- SessionResponse
- JobScrape.Application.Tests
- ComplianceConsent
- ChallengeKind
- InMemoryStore
- SourceRunContext
- JobScrape.Infrastructure.Tests
- WorkMode
- .Parse
- .Classify
- .StartDay
- JobScrape.Domain.Tests
- GlobalExceptionHandler
- JobScrape.Infrastructure
- .AddJobScrapePersistence
- .AddJobScrapeOptions
- app.tsx
- profiles.tsx
- .GetAsync
- RunCoordinator
- SqlitePragmaInterceptor
- SessionState
- OptionsTests
- .TimeProviderDelayHonorsCancellationAndRejectsNegative
- NaukriJsonLd
- BrowserProfileId
- JobScrape.Api
- SourceQuotaRepository
- SessionsEndpointTests
- SourceOptions
- ISearchProfileRepository
- .HandleAsync
- Bun BFF runtime (Bun.serve, /api/v1 proxy, SSE proxy, static hosting)
- NaukriFields
- CorrelationMiddleware
- LoggingOptions
- BrowserProfileTests
- InlineData
- .Main
- NaukriJobSource
- JobScrape.Application.Results
- SearchCriteria
- ISessionStore
- RunLimits
- .Parse
- SearchProfileTests
- FileArtifactStore
- SalaryPeriod
- .HandleAsync
- SystemRandomSource
- JobSnapshot
- RunsController
- SourcesEndpointTests
- 2. No CQRS, no MediatR
- FakeLauncher
- 3. Result pattern
- SourceCircuitBreaker
- runs.tsx
- RunReadStore
- TestDatabase
- JobLens
- .MigrateInDevelopment
- .Create
- DomainExceptionTests.cs
- PageFingerprint
- .GetAsync
- Compliance-first low-signal automation policy
- 4. Options pattern
- SearchProfileRepository
- .GetAsync
- CircuitState
- ISourceQuotaRepository
- .FindAsync
- .Configure
- .FindAsync
- Naukri parser fixtures
- NaukriTestPages
- BrowserChannel
- BrowserEngine
- QuotaKind
- .RejectsNullLocations

## God Nodes (most connected - your core abstractions)
1. `JobScrape.Domain.Common` - 106 edges
2. `ScrapeHarness` - 98 edges
3. `JobScrape.Domain.Sourcing` - 93 edges
4. `JobScrape.Domain.Access` - 70 edges
5. `JobPosting` - 70 edges
6. `SourceKey` - 66 edges
7. `ScrapeRun` - 62 edges
8. `DomainException` - 51 edges
9. `JobSnapshot` - 50 edges
10. `BrowserProfile` - 48 edges

## Surprising Connections (you probably didn't know these)
- `UI design in src/frontend` --conceptually_related_to--> `JobScrape Cockpit control plane`  [INFERRED]
  AGENTS.md → src/frontend/README.md
- `Session lifecycle state machine` --semantically_similar_to--> `BrowserProfile`  [INFERRED] [semantically similar]
  docs/07-anti-bot-and-compliance.md → docs/03-domain-model.md
- `Data minimization and privacy` --references--> `Retention Policy`  [INFERRED]
  docs/07-anti-bot-and-compliance.md → docs/09-operations-runbook.md
- `JobScrape Cockpit control plane` --cites--> `ui-skills catalog router`  [EXTRACTED]
  src/frontend/README.md → AGENTS.md
- `JobLens` --conceptually_related_to--> `Clean Architecture dependency rule`  [INFERRED]
  README.md → docs/02-architecture.md

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

## Communities (218 total, 31 thin omitted)

### Community 0 - "useRenderLog"
Cohesion: 0.15
Nodes (30): react, Abbr(), Cross(), Diamond(), Dot(), Glossary(), GlossaryTerm, Legend() (+22 more)

### Community 1 - "RunsEndpointTests.cs"
Cohesion: 0.13
Nodes (14): JobScrape.Api.Tests, JobScrape.Api.Runs, JobScrape.Api.Http, JobScrape.Application.Features.Health, JobScrape.Api.Controllers, JobScrape.Api, JobScrape.Application.Features.Sources, microsoft_aspnetcore_http (+6 more)

### Community 2 - "2. Structured logging with Serilog"
Cohesion: 0.04
Nodes (45): 1. Baseline decisions, 2.1 Layering, 2.2 Pipeline, 2.3 Bootstrap, 2.4 Enrichers, 2.5 Correlation and scopes, 2.6 Message templates and performance, 2.7 Redaction (+37 more)

### Community 3 - "package.json"
Cohesion: 0.05
Nodes (42): happy-dom, react-dom, tailwindcss, @tailwindcss/cli, @types/bun, @types/react, @types/react-dom, typescript (+34 more)

### Community 4 - "Common adapter blueprint"
Cohesion: 0.08
Nodes (30): Naukri Private Search API (jobapi/v3/search), JobSourceContractTests, Challenge kinds taxonomy, ChallengeAssessment (kind, evidence, confidence), ChallengeDetector (anti-bot section 9.3), Detection surface matrix, Public diagnostic sites (BotD, CreepJS), Acceptance matrix (+22 more)

### Community 5 - "docs/README.md"
Cohesion: 0.29
Nodes (3): Conventional Commits policy, .NET 10 LTS, ADR index

### Community 6 - "Application Patterns"
Cohesion: 0.11
Nodes (19): 1. Pattern map, 5.1 When to use it, 5.2 Shape, 5.3 Query flow, 5.4 Rules, 5.5 Testing, 5. Specification pattern, 6.1 Contract (+11 more)

### Community 7 - "Walking skeleton first"
Cohesion: 0.13
Nodes (18): Modular Monolith with Clean Architecture and inward dependency rule, Decision gates G0–G5, First 15 commits, Phase 0 — Foundation, Phase 1 — Domain + Application, Phase 2 — Browser + Session, Phase 3 — Naukri slice, Phase 4 — Indeed + LinkedIn (+10 more)

### Community 8 - ".Ok"
Cohesion: 0.10
Nodes (17): CancellationToken, Guid, Task, CancellationToken, Guid, Task, Result, Error (+9 more)

### Community 9 - "Solution Layout (JobLens.slnx)"
Cohesion: 0.09
Nodes (25): v1 Scope Boundary, v1 Success Criteria, SQLite + EF Core 10, NetArchTest architecture tests, Clean Architecture dependency rule, JobLens.Application, JobLens CLI (composition root), JobLens.Domain (+17 more)

### Community 10 - "compilerOptions"
Cohesion: 0.09
Nodes (21): compilerOptions, allowImportingTsExtensions, allowJs, isolatedModules, jsx, lib, module, moduleDetection (+13 more)

### Community 11 - "index.ts"
Cohesion: 0.13
Nodes (16): RFC-9110, src_frontend_src_client_index, CockpitConfig, parseApiUrl(), parseConfig(), parseOptionalPath(), parsePort(), apiProxy() (+8 more)

### Community 12 - "FakeSource"
Cohesion: 0.09
Nodes (31): CleanSessions, FakeSource, HttpClient, NoArtifacts, NoDelay, Profile, Session, ArgumentNullException (+23 more)

### Community 13 - "Web cockpit (v2 control plane)"
Cohesion: 0.19
Nodes (18): No Node.js policy, React + Bun web cockpit (v2.0), shadcn/ui copy-in components, HTTP/JSON commands + Server-Sent Events transport, Backend-first scaffold, Explicit Main + block namespaces, Architecture Decision Record (ADR), Bun native package manager (bun install/bun ci, bun.lock, minimumReleaseAge) (+10 more)

### Community 14 - "SearchProfileResponse"
Cohesion: 0.16
Nodes (13): DateTimeOffset, Guid, IReadOnlyList, SearchProfileResponse, CancellationToken, Guid, IReadOnlyList, OneProfileStore (+5 more)

### Community 15 - "CapturingLogger"
Cohesion: 0.21
Nodes (9): Level, EventId, Exception, Func, IDisposable, List, LogLevel, CapturingLogger (+1 more)

### Community 16 - "Playwright for .NET"
Cohesion: 0.17
Nodes (16): Headed Persistent Browser Profile, Playwright for .NET, Public Browser Diagnostics, WebKit Is Not Safari, xUnit.net v3, BrowserSessionFactory, ChallengeDetector and ChallengeAssessment, ISessionManager (+8 more)

### Community 17 - "ErrorInfo"
Cohesion: 0.14
Nodes (14): Dictionary, IResult, ErrorMapper, ArgumentNullException, Fact, InlineData, IResult, Task (+6 more)

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

### Community 23 - "ScrapeRunId"
Cohesion: 0.07
Nodes (24): ScrapeRunId, Value, DateTimeOffset, RunCancelled, DateTimeOffset, RunCompleted, DateTimeOffset, RunFailed (+16 more)

### Community 24 - "Non-negotiables"
Cohesion: 0.15
Nodes (16): Anti-ban by being a good citizen, Browser hygiene standard, Hard exclusions (no CAPTCHA solving, no spoofing), HumanPacing, Inert stealth init script finding, Low-signal automation policy, No fingerprint spoofing policy, Pacing and quotas (+8 more)

### Community 25 - "PageStats"
Cohesion: 0.14
Nodes (12): PageStats, PostingsFound, PostingsNew, PostingsUpdated, DomainException, Fact, InlineData, Theory (+4 more)

### Community 26 - ".HandleAsync"
Cohesion: 0.13
Nodes (18): CancellationToken, Guid, Task, GetSearchProfileHandler, CancellationToken, Guid, IReadOnlyList, Task (+10 more)

### Community 27 - "Per-source circuit breaker"
Cohesion: 0.22
Nodes (10): CircuitBreaker (per source), Exponential backoff policy, Challenge and block metrics, Per-source circuit breaker, Cooldown policy, doctor command, Incident runbook, Review cadence (+2 more)

### Community 28 - "RunScrapeHandler"
Cohesion: 0.13
Nodes (21): ArtifactStore, CompliancePolicy, Idempotent upsert by (SourceKey, ExternalJobId), IJobPostingRepository, JobNormalizer, JobPosting aggregate, RunScrapeHandler, TimeProvider and IRandomSource Injection (+13 more)

### Community 29 - "Cooldown Ladder"
Cohesion: 0.15
Nodes (15): Error Taxonomy and Retry Rules, ChallengeKind, ChallengePolicy, Per-Source Circuit Breaker, RetryPolicy, JobLens CLI Surface, joblens doctor, CLI Exit Codes (+7 more)

### Community 30 - "LinkedIn consent gate"
Cohesion: 0.15
Nodes (17): JobLens Bounded Contexts, CompliancePolicy, ComplianceTier, R1 LinkedIn ToS/legal exposure, R2 Indeed Cloudflare blocks, R4 Selector/DOM drift, Risk register, CompliancePolicy gate (+9 more)

### Community 31 - "http"
Cohesion: 0.20
Nodes (9): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, profiles, http (+1 more)

### Community 32 - "JobScrape Cockpit control plane"
Cohesion: 0.25
Nodes (9): UI design in src/frontend, ui-skills catalog router, BFF API proxy, .NET data plane API localhost 5207, Bun React TypeScript Tailwind stack, JobScrape Cockpit control plane, globals.css stylesheet link, main.tsx module entry (+1 more)

### Community 33 - "PolicyTests"
Cohesion: 0.07
Nodes (29): IRandomSource, ChallengeAction, Continue, ManualPause, OpenCircuit, Pause, Retry, Stop (+21 more)

### Community 34 - "Playwright for .NET"
Cohesion: 0.25
Nodes (9): xUnit.net v3, Playwright for .NET, Manual/nightly live canaries (never in CI), Local fixture server for Playwright E2E, Risk-based coverage gates (Domain 95/90, Application 90/85, overall 85/80), Test pyramid, FakeTimeProvider namespace rename, Native MTP test runner (+1 more)

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

### Community 41 - "SearchProfile"
Cohesion: 0.17
Nodes (11): DateTimeOffset, SearchProfile, CreatedAt, Criteria, Enabled, Id, Limits, Name (+3 more)

### Community 42 - ".Create"
Cohesion: 0.19
Nodes (9): ArgumentNullException, DateTimeOffset, DomainException, Fact, MemberData, Theory, TheoryData, JobPostingTests (+1 more)

### Community 43 - ".CreateAsync"
Cohesion: 0.20
Nodes (11): DbUpdateException, IMigrator, IUnitOfWork, CancellationToken, DateTimeOffset, Fact, Task, PersistenceTests (+3 more)

### Community 44 - ".Create"
Cohesion: 0.10
Nodes (16): CanonicalUrl, Value, ArgumentNullException, DomainException, Fact, MemberData, Theory, TheoryData (+8 more)

### Community 45 - "ScrapeRunTests"
Cohesion: 0.17
Nodes (7): ArgumentNullException, DateTimeOffset, DomainException, Fact, InlineData, Theory, ScrapeRunTests

### Community 46 - ".HandleAsync"
Cohesion: 0.13
Nodes (13): ActionResult, CancellationToken, HttpGet, Task, CancellationToken, Task, DateTimeOffset, SourceCircuit (+5 more)

### Community 47 - "CreateSearchProfileHandlerTests"
Cohesion: 0.20
Nodes (13): ArgumentNullException, DateTimeOffset, Fact, Handler, InlineData, InMemoryStore, IReadOnlyDictionary, Result (+5 more)

### Community 48 - "Result"
Cohesion: 0.33
Nodes (5): Result, Error, IsFailure, IsSuccess, Value

### Community 49 - "Json"
Cohesion: 0.12
Nodes (13): CriteriaJson, JsonException, JsonSerializerOptions, LimitsJson, LocationJson, SalaryJson, TimeSpan, CriteriaJson (+5 more)

### Community 50 - "api.ts"
Cohesion: 0.08
Nodes (36): RFC-9457, plural(), ApiProblem, ApiResult, createApiClient(), FetchImpl, get(), isFieldErrors() (+28 more)

### Community 51 - "JobPosting"
Cohesion: 0.07
Nodes (26): IReadOnlyList, JobPosting, ApplyUrl, Company, CompanyUrl, ContentHash, Description, ExperienceMaxYears (+18 more)

### Community 52 - "SalaryRange"
Cohesion: 0.13
Nodes (17): JobLocation, City, Country, Raw, Region, WorkMode, SalaryRange, Currency (+9 more)

### Community 53 - "JobPostingId"
Cohesion: 0.15
Nodes (8): Guid, JobPostingId, Value, Guid, DomainException, Fact, Guid, StronglyTypedIdTests

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
Cohesion: 0.26
Nodes (5): DomainException, Fact, InlineData, Theory, SearchCriteriaTests

### Community 71 - "PostingTextValueTests"
Cohesion: 0.21
Nodes (5): DomainException, Fact, InlineData, Theory, PostingTextValueTests

### Community 72 - "JobScrape.Domain.Common"
Cohesion: 0.10
Nodes (7): JobScrape.Infrastructure.Sources.Naukri, JobScrape.Infrastructure.Tests.Sources.Naukri, JobScrape.Domain.Sourcing, JobScrape.Infrastructure.Tests.Browser, JobScrape.Domain.Common, JobScrape.Domain.Abstractions, JobScrape.Domain

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
Cohesion: 0.13
Nodes (14): DateTimeOffset, TimeSpan, BrowserProfile, Channel, ConsecutiveChallenges, CooldownUntil, Engine, Id (+6 more)

### Community 90 - "SourceQuota"
Cohesion: 0.17
Nodes (12): QuotaDecision, DateOnly, DateTimeOffset, SourceQuota, Challenges, DetailsFetched, PagesFetched, QuotaDate (+4 more)

### Community 91 - "RunLimitsTests"
Cohesion: 0.33
Nodes (5): DomainException, Fact, InlineData, Theory, RunLimitsTests

### Community 92 - "RuntimeHardeningTests"
Cohesion: 0.19
Nodes (9): ValueTask, ArgumentNullException, CancellationToken, Fact, InvalidOperationException, JsonElement, Task, RuntimeHardeningTests (+1 more)

### Community 93 - "Code Conventions and csharpier workflow"
Cohesion: 0.23
Nodes (11): CI Outline restore build test E2E coverage, Code Conventions and csharpier workflow, Solution and Tooling, Options binding and startup validation, Solution and Tooling, csharpier formatting ownership, ADR-0016 C# style guides and csharpier formatting, C# style guides ktaranov plus Google (+3 more)

### Community 94 - "GuardrailTests"
Cohesion: 0.11
Nodes (14): Assembly, JobScrape.Architecture.Tests, MemberInfo, netarchtest_rules, Fact, InlineData, TestResult, Theory (+6 more)

### Community 95 - "DomainConverters"
Cohesion: 0.13
Nodes (14): CompanyName, Value, DateTimeOffset, ValidatedFields, JobSkill, Name, NormalizedName, JobTitle (+6 more)

### Community 96 - "SearchProfilesController"
Cohesion: 0.14
Nodes (14): ControllerBase, ActionResult, CancellationToken, Guid, HttpGet, HttpPost, IResult, Task (+6 more)

### Community 97 - "RunStage"
Cohesion: 0.14
Nodes (12): RunStage, Cancelled, Completed, Enriching, Failed, PausedForChallenge, Pending, Persisting (+4 more)

### Community 98 - "ref_bun_test"
Cohesion: 0.25
Nodes (8): ref_bun_test, @happy-dom/global-registrator, @testing-library/react, Compass(), cardinals(), gridLines(), TacMap(), samples

### Community 99 - "SourceCircuitBreakerTests"
Cohesion: 0.23
Nodes (5): ArgumentNullException, DateTimeOffset, DomainException, Fact, SourceCircuitBreakerTests

### Community 100 - "DataQualityIssues"
Cohesion: 0.22
Nodes (8): DataQualityIssues, DescriptionMissing, MissingCompany, MissingLocation, None, ParseLowConfidence, PostedDateUnknown, SalaryUnknown

### Community 101 - "JobScrape.Application.Abstractions"
Cohesion: 0.11
Nodes (6): JobScrape.Infrastructure.Persistence, JobScrape.Application.Abstractions, JobScrape.Infrastructure.Tests.Persistence, JobScrape.Infrastructure.Persistence.Repositories, JobScrape.Application.Features.Sessions, JobScrape.Application.Features.SearchProfiles

### Community 102 - "ADR-0014: Use Libraries as Designed; AOT Is Not a Constraint"
Cohesion: 0.25
Nodes (8): ADR-0014: Use Libraries as Designed; AOT Is Not a Constraint, Alternatives considered, Consequences, Context, Decision, Negative / trade-offs, Positive, References

### Community 103 - ".OpenScopeAsync"
Cohesion: 0.12
Nodes (10): RunIds, DateTimeOffset, Guid, IReadOnlyDictionary, Errors, ArgumentNullException, Fact, ErrorsTests (+2 more)

### Community 104 - "JobScrape.Domain.Access"
Cohesion: 0.09
Nodes (4): JobScrape.Infrastructure.Browser, JobScrape.Application.Features.Scraping, JobScrape.Domain.Access, system_collections_concurrent

### Community 105 - ".Build"
Cohesion: 0.09
Nodes (18): IHtmlDocument, INode, IParentNode, DateTimeOffset, GeneratedRegex, IDocument, Regex, NaukriDetailParser (+10 more)

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

### Community 112 - "IJobSource"
Cohesion: 0.11
Nodes (22): Akamai Bot Manager, Cloudflare Bot Management, ChallengeAssessment, ChallengeDetector, ChallengeKind, Extraction Priority (JSON-LD first), IJobSource, JobSnapshot (+14 more)

### Community 115 - "RunScrapeHandler"
Cohesion: 0.14
Nodes (19): RunScope, CancellationToken, Task, TimeSpan, Action, CancellationToken, DateTimeOffset, Exception (+11 more)

### Community 116 - "SessionHandlersTests"
Cohesion: 0.20
Nodes (12): CreateSessionRequest, DateTimeOffset, Fact, Handler, InlineData, InMemoryStore, IReadOnlyDictionary, Result (+4 more)

### Community 117 - "ScriptedSource"
Cohesion: 0.18
Nodes (9): Queue, ScriptedSource, SourceCapabilities, ScriptedSource, Capabilities, DetailRequests, Key, Requests (+1 more)

### Community 118 - "CapturingLogger"
Cohesion: 0.16
Nodes (11): ILogger, LogEntry, Dictionary, EventId, Exception, Func, IDisposable, LogLevel (+3 more)

### Community 119 - "BrowserProfileRepository"
Cohesion: 0.48
Nodes (4): CancellationToken, JobScrapeDbContext, Task, BrowserProfileRepository

### Community 120 - ".HandleAsync"
Cohesion: 0.27
Nodes (7): CancellationToken, Dictionary, Task, TimeProvider, CreateSearchProfileHandler, IReadOnlyList, CreateSearchProfileRequest

### Community 121 - "NaukriSearchParserTests"
Cohesion: 0.13
Nodes (9): IReadOnlyList, NaukriSearchPage, ArgumentNullException, Fact, InlineData, MemberData, Theory, TheoryData (+1 more)

### Community 122 - "IBrowserProfileRepository"
Cohesion: 0.17
Nodes (12): CancellationToken, Task, IBrowserProfileRepository, TimeProvider, SessionReadStore, ArgumentNullException, CancellationToken, DateTimeOffset (+4 more)

### Community 123 - ".CollapseOrNull"
Cohesion: 0.23
Nodes (6): Text, ArgumentNullException, Fact, InlineData, Theory, TextTests

### Community 124 - "FakeTimeProvider"
Cohesion: 0.17
Nodes (16): FakeTimeProvider, TimeProvider, CancelRunHandler, StartRunHandler, Guid, StartRunRequest, ArgumentNullException, CancellationToken (+8 more)

### Community 125 - "JobScrape.Infrastructure.Configuration"
Cohesion: 0.19
Nodes (4): JobScrape.Api.Composition, JobScrape.Infrastructure.Configuration, JobScrape.Infrastructure.Runtime, JobScrape.Infrastructure.Tests.Configuration

### Community 126 - "SourceKey"
Cohesion: 0.06
Nodes (36): ArgumentException, IOptions, IServiceCollection, BrowserRegistration, CancellationToken, Task, ISessionManager, IJobSource (+28 more)

### Community 127 - "JobScrapeDbContext"
Cohesion: 0.11
Nodes (16): DbContext, DbSet, IDesignTimeDbContextFactory, ModelConfigurationBuilder, DateTimeOffset, JobScrapeDbContext, BrowserProfiles, JobPostings (+8 more)

### Community 128 - "RunScope"
Cohesion: 0.17
Nodes (12): RunScope, Adapter, ArtifactDirectory, Browser, Circuit, Context, Enrich, HitWall (+4 more)

### Community 129 - "ApiFactory"
Cohesion: 0.14
Nodes (16): IWebHostBuilder, Program, Action, IServiceCollection, ApiFactory, DatabasePath, DeleteLogsOnDispose, LogDirectory (+8 more)

### Community 130 - "SessionsController"
Cohesion: 0.14
Nodes (14): ActionResult, CancellationToken, Guid, HttpGet, HttpPost, IResult, Task, SessionsController (+6 more)

### Community 131 - "CLI-first (System.CommandLine + Hosting + Serilog)"
Cohesion: 0.18
Nodes (11): .NET 10 LTS, global.json SDK pin (10.0.403, rollForward latestFeature), Clean Architecture layers (Domain, Application, Infrastructure, Cli), Focused (tactical) DDD for Sourcing and Access, Modular monolith, EF Core 10.0.14, SQLite as source of truth, WAL single-writer configuration (+3 more)

### Community 132 - "JobScrape.Api.Tests"
Cohesion: 0.20
Nodes (10): Microsoft.AspNetCore.Mvc.Testing, Serilog.Sinks.InMemory, JobScrape.Api.Tests, net10.0, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio (+2 more)

### Community 133 - "BrowserTests"
Cohesion: 0.12
Nodes (18): Html, IAsyncLifetime, SearchPageRequest, CancellationToken, DateTimeOffset, Fact, Func, InvalidOperationException (+10 more)

### Community 134 - ".ListsByNameAndGetsByIdWithoutTracking"
Cohesion: 0.29
Nodes (7): ArgumentNullException, CancellationToken, DateTimeOffset, Fact, Task, SearchProfileReadStoreTests, Ct

### Community 135 - "ExternalJobId"
Cohesion: 0.19
Nodes (8): CancellationToken, Task, ExternalJobId, Value, CancellationToken, JobScrapeDbContext, Task, JobPostingRepository

### Community 136 - "JobScrape.Architecture.Tests"
Cohesion: 0.22
Nodes (9): NetArchTest.Rules, JobScrape.Architecture.Tests, net10.0, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3 (+1 more)

### Community 137 - "SessionResponse"
Cohesion: 0.19
Nodes (11): DateTimeOffset, Guid, SessionResponse, CancellationToken, Guid, IReadOnlyList, OneSessionStore, CancellationToken (+3 more)

### Community 138 - "JobScrape.Application.Tests"
Cohesion: 0.22
Nodes (9): JobScrape.Application.Tests, net10.0, coverlet.collector, Microsoft.Extensions.TimeProvider.Testing, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3 (+1 more)

### Community 139 - "ComplianceConsent"
Cohesion: 0.14
Nodes (14): TimeProvider, ListSourcesHandler, SourceGate, SourceQuotaUsage, ArgumentNullException, CancellationToken, DateTimeOffset, Fact (+6 more)

### Community 140 - "ChallengeKind"
Cohesion: 0.13
Nodes (13): ChallengeKind, AccessDenied, Captcha, LoginRequired, None, RateLimited, SoftChallenge, SourceChanged (+5 more)

### Community 141 - "InMemoryStore"
Cohesion: 0.09
Nodes (23): CancellationToken, CancellationTokenSource, DateOnly, IReadOnlyList, List, Task, TimeSpan, InMemoryStore (+15 more)

### Community 142 - "SourceRunContext"
Cohesion: 0.11
Nodes (19): CancellationToken, Task, CancellationToken, Task, JobReference, SourceRunContext, CancellationToken, Task (+11 more)

### Community 143 - "JobScrape.Infrastructure.Tests"
Cohesion: 0.22
Nodes (9): JobScrape.Infrastructure.Tests, net10.0, coverlet.collector, Microsoft.Extensions.TimeProvider.Testing, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3 (+1 more)

### Community 144 - "WorkMode"
Cohesion: 0.33
Nodes (5): WorkMode, Hybrid, Onsite, Remote, Unknown

### Community 145 - ".Parse"
Cohesion: 0.25
Nodes (5): DomainException, Fact, InlineData, Theory, SourceKeyTests

### Community 146 - ".Classify"
Cohesion: 0.13
Nodes (13): BodyText, Match, Title, GeneratedRegex, Regex, Uri, NaukriChallengeClassifier, ArgumentNullException (+5 more)

### Community 147 - ".StartDay"
Cohesion: 0.31
Nodes (5): ArgumentNullException, DateTimeOffset, DomainException, Fact, SourceQuotaTests

### Community 148 - "JobScrape.Domain.Tests"
Cohesion: 0.25
Nodes (8): JobScrape.Domain.Tests, net10.0, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3, Microsoft.NET.Sdk

### Community 149 - "GlobalExceptionHandler"
Cohesion: 0.24
Nodes (9): IExceptionHandler, PathString, CancellationToken, Exception, HttpContext, ILogger, LoggerMessage, ValueTask (+1 more)

### Community 150 - "JobScrape.Infrastructure"
Cohesion: 0.22
Nodes (9): AngleSharp, Microsoft.EntityFrameworkCore.Design, Microsoft.EntityFrameworkCore.Sqlite, Microsoft.Extensions.Options.DataAnnotations, Microsoft.Playwright, JobScrape.Infrastructure, net10.0, Microsoft.Extensions.Logging.Abstractions (+1 more)

### Community 151 - ".AddJobScrapePersistence"
Cohesion: 0.27
Nodes (10): IJobPostingRepository, IScrapeRunRepository, ISourceCircuitBreakerRepository, ScrapeStores, IServiceCollection, PersistenceServiceCollectionExtensions, JobScrapeDbContext, ScrapeRunRepository (+2 more)

### Community 152 - ".AddJobScrapeOptions"
Cohesion: 0.14
Nodes (14): IValidateOptions, IOptions, IServiceCollection, IValidateOptions, OptionsRegistration, ComplianceOptions, AcknowledgeSourceRisk, AllowHighRiskSources (+6 more)

### Community 153 - "app.tsx"
Cohesion: 0.12
Nodes (29): App(), checkHealth(), createProfile(), createSession(), loadProfiles(), loadSessions(), loadSources(), clockTime() (+21 more)

### Community 154 - "profiles.tsx"
Cohesion: 0.15
Nodes (17): codeTone(), Field(), CreateProfileInput, CreateSessionInput, CodeRow(), orDash(), ProfilesPage(), submit() (+9 more)

### Community 155 - ".GetAsync"
Cohesion: 0.06
Nodes (28): HealthPayload, IClassFixture, OptionsValidationException, ActionResult, CancellationToken, HttpGet, Task, HealthController (+20 more)

### Community 156 - "RunCoordinator"
Cohesion: 0.11
Nodes (22): ActiveRun, IDisposable, IHostedService, IServiceScopeFactory, Lock, BrowserProfileId, CancellationToken, CancellationTokenSource (+14 more)

### Community 157 - "SqlitePragmaInterceptor"
Cohesion: 0.28
Nodes (6): ConnectionEndEventData, DbConnection, DbConnectionInterceptor, CancellationToken, Task, SqlitePragmaInterceptor

### Community 158 - "SessionState"
Cohesion: 0.18
Nodes (9): DateTimeOffset, SessionBootstrapped, SessionState, Challenged, CoolingDown, Expired, Healthy, New (+1 more)

### Community 159 - "OptionsTests"
Cohesion: 0.36
Nodes (4): Dictionary, Fact, ValidateOptionsResult, OptionsTests

### Community 160 - ".TimeProviderDelayHonorsCancellationAndRejectsNegative"
Cohesion: 0.21
Nodes (9): CancellationToken, Task, TimeProvider, TimeSpan, TimeProviderDelay, ArgumentNullException, ArgumentOutOfRangeException, OperationCanceledException (+1 more)

### Community 161 - "NaukriJsonLd"
Cohesion: 0.10
Nodes (21): JsonDocument, JsonDocumentOptions, IDocument, IEnumerable, IReadOnlyList, JsonElement, NaukriJsonLd, Company (+13 more)

### Community 162 - "BrowserProfileId"
Cohesion: 0.09
Nodes (21): ConcurrentDictionary, IBrowserContext, IBrowserType, IPlaywright, SemaphoreSlim, Guid, BrowserProfileId, Value (+13 more)

### Community 163 - "JobScrape.Api"
Cohesion: 0.15
Nodes (13): Microsoft.AspNetCore.OpenApi, Serilog.AspNetCore, Serilog.Enrichers.AssemblyName, Serilog.Enrichers.Demystifier, Serilog.Enrichers.Environment, Serilog.Enrichers.Process, Serilog.Enrichers.Span, Serilog.Enrichers.Thread (+5 more)

### Community 164 - "SourceQuotaRepository"
Cohesion: 0.38
Nodes (5): CancellationToken, DateOnly, JobScrapeDbContext, Task, SourceQuotaRepository

### Community 165 - "SessionsEndpointTests"
Cohesion: 0.31
Nodes (6): ArgumentNullException, CancellationToken, Fact, Task, SessionsEndpointTests, Ct

### Community 166 - "SourceOptions"
Cohesion: 0.15
Nodes (12): SourceOptions, BaseUrl, ChallengeCooldownMinutes, DailyDetailCap, DailyPageCap, DailyRunCap, Enabled, MaxDelayMs (+4 more)

### Community 167 - "ISearchProfileRepository"
Cohesion: 0.27
Nodes (9): ISearchProfileRepository, ArgumentNullException, ArgumentOutOfRangeException, CancellationToken, DateTimeOffset, Fact, Task, RunReadStoreTests (+1 more)

### Community 168 - ".HandleAsync"
Cohesion: 0.20
Nodes (7): CancellationToken, Task, CancellationToken, Task, TimeProvider, CreateSessionHandler, ArgumentNullException

### Community 169 - "Bun BFF runtime (Bun.serve, /api/v1 proxy, SSE proxy, static hosting)"
Cohesion: 0.29
Nodes (7): Same-origin Bun BFF, Cockpit auth: loopback bind, HttpOnly cookie, CSRF double-submit, BFF bearer token, Bun BFF runtime (Bun.serve, /api/v1 proxy, SSE proxy, static hosting), Web Cockpit Control Plane, CSP and security headers, .NET API Data Plane, SSE Run Event Stream

### Community 170 - "NaukriFields"
Cohesion: 0.20
Nodes (4): GeneratedRegex, Regex, Uri, NaukriFields

### Community 171 - "CorrelationMiddleware"
Cohesion: 0.24
Nodes (6): RequestDelegate, HttpContext, Task, CorrelationMiddleware, InlineData, Theory

### Community 172 - "LoggingOptions"
Cohesion: 0.20
Nodes (8): IConfiguration, IOptions, IServiceCollection, LoggingRegistration, LoggingOptions, Directory, FileSizeLimitBytes, RetentionDays

### Community 173 - "BrowserProfileTests"
Cohesion: 0.23
Nodes (6): ArgumentNullException, DateTimeOffset, DomainException, Fact, TimeSpan, BrowserProfileTests

### Community 175 - ".Main"
Cohesion: 0.15
Nodes (12): ILogger, Program, CancellationToken, Guid, Task, GetRunHandler, CancellationToken, Guid (+4 more)

### Community 176 - "NaukriJobSource"
Cohesion: 0.17
Nodes (14): IPage, Task, PageText, CancellationToken, Exception, Guid, ILogger, IPage (+6 more)

### Community 177 - "JobScrape.Application.Results"
Cohesion: 0.15
Nodes (5): JobScrape.Application.Results, JobScrape.Application.Tests, JobScrape.Application.Features.Runs, JobScrape.Application.Tests.Scraping, microsoft_extensions_time_testing

### Community 178 - "SearchCriteria"
Cohesion: 0.14
Nodes (14): IReadOnlyList, SearchCriteria, ExperienceYears, Keywords, Locations, PostedWithinDays, RemoteOnly, Uri (+6 more)

### Community 179 - "ISessionStore"
Cohesion: 0.32
Nodes (5): CancellationToken, Guid, IReadOnlyList, Task, ISessionStore

### Community 180 - "RunLimits"
Cohesion: 0.25
Nodes (7): TimeSpan, RunLimits, MaxDelay, MaxDetails, MaxJobs, MaxPages, MinDelay

### Community 181 - ".Parse"
Cohesion: 0.23
Nodes (5): ArgumentNullException, Fact, InlineData, Theory, NaukriDetailParserTests

### Community 182 - "SearchProfileTests"
Cohesion: 0.20
Nodes (9): DateTimeOffset, SearchProfileUpdated, ArgumentNullException, DateTimeOffset, DomainException, Fact, InlineData, Theory (+1 more)

### Community 183 - "FileArtifactStore"
Cohesion: 0.18
Nodes (10): TimeSpan, BrowserSessionSettings, CancellationToken, Exception, Guid, ILogger, LoggerMessage, Task (+2 more)

### Community 184 - "SalaryPeriod"
Cohesion: 0.29
Nodes (6): SalaryPeriod, Day, Hour, Month, Week, Year

### Community 185 - ".HandleAsync"
Cohesion: 0.15
Nodes (10): CancellationToken, Guid, Task, Guid, IReadOnlyList, RunPostingListResponse, DateTimeOffset, Guid (+2 more)

### Community 187 - "JobSnapshot"
Cohesion: 0.09
Nodes (22): DateTimeOffset, IReadOnlyList, JobSnapshot, ApplyUrl, Company, CompanyUrl, Description, ExperienceMaxYears (+14 more)

### Community 188 - "RunsController"
Cohesion: 0.20
Nodes (12): ActionResult, CancellationToken, Guid, HttpGet, HttpPost, IResult, Task, RunsController (+4 more)

### Community 189 - "SourcesEndpointTests"
Cohesion: 0.27
Nodes (7): SourcesController, ArgumentNullException, CancellationToken, Fact, Task, SourcesEndpointTests, Ct

### Community 190 - "2. No CQRS, no MediatR"
Cohesion: 0.29
Nodes (7): 2.1 Decision, 2.2 Banned, 2.3 Replacements, 2.4 Why, 2.5 Allowed read/write separation, 2.6 Enforcement, 2. No CQRS, no MediatR

### Community 191 - "FakeLauncher"
Cohesion: 0.14
Nodes (15): CancellationToken, Guid, Task, IRunLauncher, CancellationToken, Task, RunScrapeRequest, Guid (+7 more)

### Community 192 - "3. Result pattern"
Cohesion: 0.29
Nodes (7): 3.1 When to use it, 3.2 Shape, 3.3 Error codes, 3.4 Handler example, 3.5 Rules, 3.6 Testing, 3. Result pattern

### Community 193 - "SourceCircuitBreaker"
Cohesion: 0.19
Nodes (11): DateTimeOffset, IReadOnlyList, List, TimeSpan, SourceCircuitBreaker, AllowsRun, ConsecutiveFailures, OpenUntil (+3 more)

### Community 194 - "runs.tsx"
Cohesion: 0.24
Nodes (15): constantCase(), RunPayload, clock(), finishedEvent(), RUN_POLL_MS, RUN_STAGES, RunDetail(), runDuration() (+7 more)

### Community 195 - "RunReadStore"
Cohesion: 0.35
Nodes (7): CancellationToken, DateTimeOffset, Dictionary, Guid, Task, TimeProvider, RunReadStore

### Community 196 - "TestDatabase"
Cohesion: 0.18
Nodes (10): AsyncServiceScope, IAsyncDisposable, ServiceProvider, ValueTask, TestDatabase, ConnectionString, Path, TestScope (+2 more)

### Community 197 - "JobLens"
Cohesion: 0.15
Nodes (14): ADR-0010 React + Bun cockpit, v2 Web Cockpit, v2 track — Web cockpit, AngleSharp, ClosedXML, Documentation reading order, .NET 10 LTS, JobLens (+6 more)

### Community 198 - ".MigrateInDevelopment"
Cohesion: 0.21
Nodes (8): IConfiguration, IServiceCollection, DatabaseRegistration, ArgumentNullException, Fact, Task, DatabaseRegistrationTests, WebApplication

### Community 199 - ".Create"
Cohesion: 0.18
Nodes (7): CancellationToken, Task, Guid, SearchProfileId, Value, DateTimeOffset, SearchProfileCreated

### Community 200 - "DomainExceptionTests.cs"
Cohesion: 0.40
Nodes (3): JobScrape.Domain.Tests, Fact, DomainExceptionTests

### Community 201 - "PageFingerprint"
Cohesion: 0.33
Nodes (6): PageFingerprint, FirstExternalId, ResultRange, DomainException, Fact, PageFingerprintTests

### Community 202 - ".GetAsync"
Cohesion: 0.47
Nodes (3): CancellationToken, IReadOnlyList, Task

### Community 203 - "Compliance-first low-signal automation policy"
Cohesion: 0.29
Nodes (7): Headed first contact + persistent contexts per source, Browser-first automation as source of truth, Optional Naukri API fast path (jobapi/v3/search with RSA nkparam), Compliance-first low-signal automation policy, Stop-on-wall with manual challenge solve, Normal network egress, no rotation (D8), Challenge handoff to host headed browser

### Community 204 - "4. Options pattern"
Cohesion: 0.33
Nodes (6): 4.1 Boundary, 4.2 Bind and validate, 4.3 Lifetimes, 4.4 Secrets, 4.5 Testing, 4. Options pattern

### Community 205 - "SearchProfileRepository"
Cohesion: 0.48
Nodes (4): CancellationToken, JobScrapeDbContext, Task, SearchProfileRepository

### Community 206 - ".GetAsync"
Cohesion: 0.47
Nodes (3): CancellationToken, IReadOnlyList, Task

### Community 207 - "CircuitState"
Cohesion: 0.40
Nodes (4): CircuitState, Closed, HalfOpen, Open

### Community 208 - "ISourceQuotaRepository"
Cohesion: 0.47
Nodes (4): CancellationToken, DateOnly, Task, ISourceQuotaRepository

### Community 210 - ".Configure"
Cohesion: 0.50
Nodes (3): DateTimeOffset, EntityTypeBuilder, List

### Community 212 - "Naukri parser fixtures"
Cohesion: 0.50
Nodes (3): Files, Naukri parser fixtures, Selectors (version 1)

### Community 214 - "BrowserChannel"
Cohesion: 0.40
Nodes (4): BrowserChannel, Bundled, Chrome, Edge

### Community 215 - "BrowserEngine"
Cohesion: 0.40
Nodes (4): BrowserEngine, Chromium, Firefox, WebKit

### Community 216 - "QuotaKind"
Cohesion: 0.40
Nodes (4): QuotaKind, Details, Pages, Runs

## Knowledge Gaps
- **772 isolated node(s):** `$schema`, `type`, `url`, `oauth`, `Authorization` (+767 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1265 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **31 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `SourceKey` connect `SourceKey` to `ExternalJobId`, `FakeSource`, `InMemoryStore`, `SourceRunContext`, `ChallengeKind`, `.Parse`, `.StartDay`, `ScrapeHarness`, `ScrapeRunId`, `SessionState`, `PolicyTests`, `BrowserProfileId`, `SourceQuotaRepository`, `SearchProfile`, `.CreateAsync`, `.Create`, `BrowserProfileTests`, `NaukriJobSource`, `JobPosting`, `SearchProfileTests`, `JobSnapshot`, `SourceCircuitBreaker`, `.Create`, `ISourceQuotaRepository`, `.FindAsync`, `.FindAsync`, `ContentHash`, `ScrapeRun`, `BrowserProfile`, `SourceQuota`, `DomainConverters`, `SourceCircuitBreakerTests`, `.OpenScopeAsync`, `RunScrapeHandler`, `ScriptedSource`?**
  _High betweenness centrality (0.076) - this node is a cross-community bridge._
- **Why does `JobPosting` connect `JobPosting` to `RunScope`, `ExternalJobId`, `InMemoryStore`, `.Classify`, `ScrapeHarness`, `ISearchProfileRepository`, `.Create`, `.Create`, `SalaryRange`, `JobPostingId`, `RunReadStore`, `JobScrape.Domain.Common`, `AggregateRoot`, `ContentHash`, `.OnModelCreating`, `ScrapeRun`, `DomainConverters`, `DataQualityIssues`, `JobPostingStatus`, `RunScrapeHandler`, `NaukriSearchParserTests`, `SourceKey`, `JobScrapeDbContext`?**
  _High betweenness centrality (0.047) - this node is a cross-community bridge._
- **Why does `JobSnapshot` connect `JobSnapshot` to `ISearchProfileRepository`, `JobScrape.Domain.Common`, `.Build`, `.Create`, `.CreateAsync`, `FakeSource`, `SourceRunContext`, `RunScrapeHandler`, `JobPosting`, `SalaryRange`, `ScrapeHarness`, `.Parse`, `ScrapeRun`, `NaukriSearchParserTests`, `SourceKey`?**
  _High betweenness centrality (0.040) - this node is a cross-community bridge._
- **What connects `$schema`, `type`, `url` to the rest of the system?**
  _772 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `RunsEndpointTests.cs` be split into smaller, more focused modules?**
  _Cohesion score 0.1268939393939394 - nodes in this community are weakly interconnected._
- **Should `2. Structured logging with Serilog` be split into smaller, more focused modules?**
  _Cohesion score 0.044444444444444446 - nodes in this community are weakly interconnected._
- **Should `package.json` be split into smaller, more focused modules?**
  _Cohesion score 0.046511627906976744 - nodes in this community are weakly interconnected._