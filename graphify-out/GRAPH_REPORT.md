# Graph Report - PlaywrightDotNet  (2026-11-01)

## Corpus Check
- 273 files · ~129,022 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 2684 nodes · 5617 edges · 187 communities (152 shown, 35 thin omitted)
- Extraction: 92% EXTRACTED · 8% INFERRED · 0% AMBIGUOUS · INFERRED: 477 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `2aa09bce`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- overview.tsx
- SearchProfilesEndpointTests.cs
- 2. Structured logging with Serilog
- package.json
- Common adapter blueprint
- docs/README.md
- Application Patterns
- JobLens
- .Fail
- Solution Layout (JobLens.slnx)
- compilerOptions
- index.ts
- RunScrapeHandler
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
- HealthEndpointTests
- Non-negotiables
- CLI-first (System.CommandLine + Hosting + Serilog)
- .HandleAsync
- Per-source circuit breaker
- JobPosting
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
- .CreateAsync
- .Create
- ScrapeRunTests
- Bun BFF runtime (Bun.serve, /api/v1 proxy, SSE proxy, static hosting)
- .ValidationFailed
- Result
- Json
- RunStats
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
- PageStats
- Cockpit Favicon Icon
- .OnModelCreating
- ScrapeRun
- BrowserProfile
- SourceQuota
- RunLimitsTests
- RuntimeHardeningTests
- ADR index
- GuardrailTests
- DomainConverters
- .GetAsync
- RunStage
- JobScrape.Application.Abstractions
- SourceCircuitBreakerTests
- DataQualityIssues
- useRenderLog
- ADR-0014: Use Libraries as Designed; AOT Is Not a Constraint
- ErrorInfo
- JobScrape.Domain.Access
- DomainExceptionTests.cs
- graphify.js
- xunit
- SearchPageParser
- InitialCreate
- JobPostingStatus
- .AddJobScrapeOptions
- JobScrapeDbContext
- jobscrape_api
- jobscrape_application_features_health
- .EnrichAsync
- TestDatabase
- RunScrapeHandler
- InMemoryStore
- BrowserProfileId
- app.tsx
- RunLimits
- SearchCriteria
- SqlitePragmaInterceptor
- CapturingLogger
- JobScrape.Infrastructure.Configuration
- SourceSettingsCatalog
- ScriptedSource
- RunScope
- ApiFactory
- SearchProfile
- PageFingerprint
- JobScrape.Api.Tests
- SourceRunContext
- api.ts
- ExternalJobId
- JobScrape.Architecture.Tests
- SourceKey
- JobScrape.Application.Tests
- RunPausedForChallenge
- SalaryPeriod
- CancellationToken
- RunScrapeOnSqliteTests
- JobScrape.Infrastructure.Tests
- WorkMode
- .GetAsync
- JobLocation
- .TwoRunsPersistDedupeAndChargeTheDailyQuota
- JobScrape.Domain.Tests
- ChallengeAction
- JobScrape.Infrastructure
- SourceCircuitBreaker
- SourcesOptions
- ComplianceTier
- .GetAsync
- .Parse
- CanonicalUrl
- scripts
- .StartDay
- OptionsTests
- .TimeProviderDelayHonorsCancellationAndRejectsNegative
- Naukri Playbook
- QuotaDecision
- JobScrape.Api
- PersistenceTests
- ChallengeKind
- SourceOptions
- CapturingLogger
- SearchProfileId
- GlobalExceptionHandler
- tacmap.tsx
- ChallengeDetector (anti-bot section 9.3)
- .AddJobScrapeLogging
- ResultOfTTests
- SourceQuotaRepository
- IDelay
- JobLens.Application
- ErrorInfoTests
- IRandomSource
- CircuitState
- SQLite Backup and Restore
- .FindAsync
- .Configure
- .FindAsync
- .SaveChangesAsync
- .NonWallsAreRejected
- SystemRandomSource

## God Nodes (most connected - your core abstractions)
1. `ScrapeHarness` - 96 edges
2. `JobScrape.Domain.Common` - 80 edges
3. `JobScrape.Domain.Sourcing` - 70 edges
4. `JobPosting` - 66 edges
5. `SourceKey` - 61 edges
6. `ScrapeRun` - 56 edges
7. `JobScrape.Domain.Access` - 54 edges
8. `DomainException` - 51 edges
9. `RunScrapeHandlerTests` - 45 edges
10. `RunScrapeHandler` - 41 edges

## Surprising Connections (you probably didn't know these)
- `UI design in src/frontend` --conceptually_related_to--> `JobScrape Cockpit control plane`  [INFERRED]
  AGENTS.md → src/frontend/README.md
- `SearchPageResult` --references--> `PageFingerprint`  [INFERRED]
  docs/02-architecture.md → docs/03-domain-model.md
- `Session lifecycle state machine` --semantically_similar_to--> `BrowserProfile`  [INFERRED] [semantically similar]
  docs/07-anti-bot-and-compliance.md → docs/03-domain-model.md
- `Consistency beats spoofing` --semantically_similar_to--> `No fingerprint spoofing policy`  [INFERRED] [semantically similar]
  docs/README.md → docs/07-anti-bot-and-compliance.md
- `RunScrapeHandler` --references--> `QuotaPolicy`  [INFERRED]
  docs/02-architecture.md → docs/03-domain-model.md

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

## Communities (187 total, 35 thin omitted)

### Community 0 - "overview.tsx"
Cohesion: 0.18
Nodes (20): Abbr(), Cross(), Diamond(), Dot(), Glossary(), GlossaryTerm, Legend(), LegendItem (+12 more)

### Community 1 - "SearchProfilesEndpointTests.cs"
Cohesion: 0.11
Nodes (15): JobScrape.Api.Tests, JobScrape.Infrastructure.SearchProfiles, JobScrape.Api.Http, JobScrape.Api.Controllers, JobScrape.Api, JobScrape.Application.Features.SearchProfiles, microsoft_aspnetcore_http, microsoft_aspnetcore_mvc_testing (+7 more)

### Community 2 - "2. Structured logging with Serilog"
Cohesion: 0.04
Nodes (45): 1. Baseline decisions, 2.1 Layering, 2.2 Pipeline, 2.3 Bootstrap, 2.4 Enrichers, 2.5 Correlation and scopes, 2.6 Message templates and performance, 2.7 Redaction (+37 more)

### Community 3 - "package.json"
Cohesion: 0.07
Nodes (26): happy-dom, react-dom, tailwindcss, @tailwindcss/cli, @types/bun, @types/react, @types/react-dom, typescript (+18 more)

### Community 4 - "Common adapter blueprint"
Cohesion: 0.10
Nodes (24): JobSourceContractTests, Challenge kinds taxonomy, ChallengeAssessment (kind, evidence, confidence), Acceptance matrix, Common adapter blueprint, JobSourceContractTests, Data-quality flags, DetailParser (+16 more)

### Community 6 - "Application Patterns"
Cohesion: 0.05
Nodes (39): 1. Pattern map, 2.1 Decision, 2.2 Banned, 2.3 Replacements, 2.4 Why, 2.5 Allowed read/write separation, 2.6 Enforcement, 2. No CQRS, no MediatR (+31 more)

### Community 7 - "JobLens"
Cohesion: 0.06
Nodes (38): ADR-0010 React + Bun cockpit, Modular Monolith with Clean Architecture and inward dependency rule, v2 Web Cockpit, Decision gates G0–G5, First 15 commits, Phase 0 — Foundation, Phase 1 — Domain + Application, Phase 2 — Browser + Session (+30 more)

### Community 8 - ".Fail"
Cohesion: 0.20
Nodes (8): Result, Error, IsFailure, IsSuccess, ArgumentNullException, Fact, InvalidOperationException, ResultTests

### Community 9 - "Solution Layout (JobLens.slnx)"
Cohesion: 0.18
Nodes (14): v1 Scope Boundary, v1 Success Criteria, NetArchTest architecture tests, Clean Architecture dependency rule, JobLens.Domain, CI Pipeline, Deliberately Absent Packages, Package Matrix central package management (+6 more)

### Community 10 - "compilerOptions"
Cohesion: 0.09
Nodes (21): compilerOptions, allowImportingTsExtensions, allowJs, isolatedModules, jsx, lib, module, moduleDetection (+13 more)

### Community 11 - "index.ts"
Cohesion: 0.15
Nodes (13): RFC-9110, src_frontend_src_client_index, CockpitConfig, parseApiUrl(), parseConfig(), parsePort(), apiProxy(), createHandler() (+5 more)

### Community 12 - "RunScrapeHandler"
Cohesion: 0.16
Nodes (18): ArtifactStore, ChallengeAssessment, ChallengeDetector, ChallengeKind, CompliancePolicy, Error Taxonomy and Retry Rules, IJobSource, JobSnapshot (+10 more)

### Community 13 - "Web cockpit (v2 control plane)"
Cohesion: 0.19
Nodes (18): No Node.js policy, React + Bun web cockpit (v2.0), shadcn/ui copy-in components, HTTP/JSON commands + Server-Sent Events transport, Backend-first scaffold, Explicit Main + block namespaces, Architecture Decision Record (ADR), Bun native package manager (bun install/bun ci, bun.lock, minimumReleaseAge) (+10 more)

### Community 14 - "SearchProfileResponse"
Cohesion: 0.23
Nodes (11): ConcurrentDictionary, ArgumentNullException, Fact, Task, InMemorySearchProfileStoreTests, Guid, SearchProfileResponse, CancellationToken (+3 more)

### Community 15 - ".GetAsync"
Cohesion: 0.12
Nodes (14): ActionResult, CancellationToken, HttpGet, Task, CancellationToken, Task, TimeProvider, GetHealthHandler (+6 more)

### Community 16 - "Playwright for .NET"
Cohesion: 0.17
Nodes (16): Headed Persistent Browser Profile, Playwright for .NET, Public Browser Diagnostics, WebKit Is Not Safari, xUnit.net v3, BrowserSessionFactory, ChallengeDetector and ChallengeAssessment, ISessionManager (+8 more)

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
Nodes (52): CapturingLogger, FakeSessions, InMemoryStore, RecordingArtifacts, RecordingDelay, JobDetailResult, DateTimeOffset, Guid (+44 more)

### Community 23 - "HealthEndpointTests"
Cohesion: 0.12
Nodes (13): HealthPayload, IClassFixture, OptionsValidationException, ArgumentNullException, DateTimeOffset, Fact, Task, HealthEndpointTests (+5 more)

### Community 24 - "Non-negotiables"
Cohesion: 0.36
Nodes (8): Anti-ban by being a good citizen, Hard exclusions (no CAPTCHA solving, no spoofing), Low-signal automation policy, Stop-on-wall hard rule, Consistency beats spoofing, No evasion tooling, No browser stack is undetectable, Non-negotiables

### Community 25 - "CLI-first (System.CommandLine + Hosting + Serilog)"
Cohesion: 0.18
Nodes (11): .NET 10 LTS, global.json SDK pin (10.0.403, rollForward latestFeature), Clean Architecture layers (Domain, Application, Infrastructure, Cli), Focused (tactical) DDD for Sourcing and Access, Modular monolith, EF Core 10.0.14, SQLite as source of truth, WAL single-writer configuration (+3 more)

### Community 26 - ".HandleAsync"
Cohesion: 0.13
Nodes (18): ILogger, Program, CancellationToken, Guid, Task, GetSearchProfileHandler, CancellationToken, Guid (+10 more)

### Community 27 - "Per-source circuit breaker"
Cohesion: 0.22
Nodes (10): CircuitBreaker (per source), Exponential backoff policy, Challenge and block metrics, Per-source circuit breaker, Cooldown policy, doctor command, Incident runbook, Review cadence (+2 more)

### Community 28 - "JobPosting"
Cohesion: 0.13
Nodes (19): Idempotent upsert by (SourceKey, ExternalJobId), IJobPostingRepository, JobNormalizer, JobPosting aggregate, TimeProvider and IRandomSource Injection, JobLens Bounded Contexts, ContentHash, DeduplicationPolicy (+11 more)

### Community 29 - "Cooldown Ladder"
Cohesion: 0.17
Nodes (13): Per-Source Circuit Breaker, JobLens CLI Surface, joblens doctor, CLI Exit Codes, Cooldown Ladder, Data minimization and privacy, Anti-Bot Metrics and Alerts, Retention Policy (+5 more)

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
Cohesion: 0.12
Nodes (16): ComplianceConsent, None, ComplianceDecision, CompliancePolicy, PolitenessPolicy, TimeSpan, RetryPolicy, ArgumentNullException (+8 more)

### Community 34 - "Playwright for .NET"
Cohesion: 0.17
Nodes (13): xUnit.net v3, Playwright for .NET, Browser-first automation as source of truth, Optional Naukri API fast path (jobapi/v3/search with RSA nkparam), Manual/nightly live canaries (never in CI), Local fixture server for Playwright E2E, Risk-based coverage gates (Domain 95/90, Application 90/85, overall 85/80), Test pyramid (+5 more)

### Community 35 - "Phase 0 open decisions (D1-D12 with recommended defaults)"
Cohesion: 0.19
Nodes (14): Headed first contact + persistent contexts per source, Compliance-first low-signal automation policy, Per-source compliance tiers and consent gates, Stop-on-wall with manual challenge solve, CSV + Excel export (JSON optional, Postgres later), Data retention 30/90/180 days (D12), Full posting field set (D4), Indeed opt-in with conservative caps (D3) (+6 more)

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
Cohesion: 0.06
Nodes (42): IEnumerable, ContentHash, Value, DateTimeOffset, JobPostingUpdated, DateTimeOffset, IReadOnlyList, JobSnapshot (+34 more)

### Community 43 - ".CreateAsync"
Cohesion: 0.22
Nodes (9): DbUpdateException, IMigrator, ISearchProfileRepository, ISourceQuotaRepository, IUnitOfWork, Fact, Task, CancellationToken (+1 more)

### Community 44 - ".Create"
Cohesion: 0.20
Nodes (9): ArgumentNullException, DomainException, Fact, MemberData, Theory, TheoryData, CanonicalUrlTests, PlaybookExamples (+1 more)

### Community 45 - "ScrapeRunTests"
Cohesion: 0.20
Nodes (5): ArgumentNullException, DateTimeOffset, DomainException, Fact, ScrapeRunTests

### Community 46 - "Bun BFF runtime (Bun.serve, /api/v1 proxy, SSE proxy, static hosting)"
Cohesion: 0.29
Nodes (7): Same-origin Bun BFF, Cockpit auth: loopback bind, HttpOnly cookie, CSRF double-submit, BFF bearer token, Bun BFF runtime (Bun.serve, /api/v1 proxy, SSE proxy, static hosting), Web Cockpit Control Plane, CSP and security headers, .NET API Data Plane, SSE Run Event Stream

### Community 47 - ".ValidationFailed"
Cohesion: 0.39
Nodes (4): IReadOnlyDictionary, ArgumentNullException, Fact, ErrorsTests

### Community 48 - "Result"
Cohesion: 0.33
Nodes (5): Result, Error, IsFailure, IsSuccess, Value

### Community 49 - "Json"
Cohesion: 0.13
Nodes (12): CriteriaJson, JsonException, JsonSerializerOptions, LimitsJson, SalaryJson, TimeSpan, CriteriaJson, Json (+4 more)

### Community 50 - "RunStats"
Cohesion: 0.07
Nodes (23): Guid, ScrapeRunId, Value, DateTimeOffset, RunCancelled, DateTimeOffset, RunCompleted, DateTimeOffset (+15 more)

### Community 51 - "JobPosting"
Cohesion: 0.07
Nodes (27): IReadOnlyList, JobPosting, ApplyUrl, Company, CompanyUrl, ContentHash, Description, ExperienceMaxYears (+19 more)

### Community 52 - "SalaryRange"
Cohesion: 0.20
Nodes (10): SalaryRange, Currency, Max, Min, Period, DomainException, Fact, InlineData (+2 more)

### Community 53 - "JobPostingId"
Cohesion: 0.13
Nodes (9): Guid, Guid, JobPostingId, Value, Guid, DomainException, Fact, Guid (+1 more)

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
Cohesion: 0.12
Nodes (11): Text, ArgumentNullException, Fact, InlineData, Theory, TextTests, DomainException, Fact (+3 more)

### Community 72 - "JobScrape.Domain.Common"
Cohesion: 0.15
Nodes (3): JobScrape.Domain.Sourcing, JobScrape.Domain.Common, JobScrape.Domain.Abstractions

### Community 83 - "AggregateRoot"
Cohesion: 0.17
Nodes (13): IList, IReadOnlyList, List, AggregateRoot, DomainEvents, DateTimeOffset, DomainEvent, ArgumentNullException (+5 more)

### Community 85 - "PageStats"
Cohesion: 0.14
Nodes (12): PageStats, PostingsFound, PostingsNew, PostingsUpdated, DomainException, Fact, InlineData, Theory (+4 more)

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
Cohesion: 0.14
Nodes (13): CancellationToken, DateOnly, Task, DateOnly, SourceQuota, Challenges, DetailsFetched, PagesFetched (+5 more)

### Community 91 - "RunLimitsTests"
Cohesion: 0.33
Nodes (5): DomainException, Fact, InlineData, Theory, RunLimitsTests

### Community 92 - "RuntimeHardeningTests"
Cohesion: 0.12
Nodes (15): JsonElement, RequestDelegate, HttpContext, Task, CorrelationMiddleware, ValueTask, ArgumentNullException, CancellationToken (+7 more)

### Community 93 - "ADR index"
Cohesion: 0.23
Nodes (12): CI Outline restore build test E2E coverage, Code Conventions and csharpier workflow, Solution and Tooling, Options binding and startup validation, Solution and Tooling, csharpier formatting ownership, ADR-0016 C# style guides and csharpier formatting, C# style guides ktaranov plus Google (+4 more)

### Community 94 - "GuardrailTests"
Cohesion: 0.11
Nodes (14): Assembly, JobScrape.Architecture.Tests, MemberInfo, netarchtest_rules, Fact, InlineData, TestResult, Theory (+6 more)

### Community 95 - "DomainConverters"
Cohesion: 0.14
Nodes (14): CompanyName, Value, DateTimeOffset, ValidatedFields, JobSkill, Name, NormalizedName, JobTitle (+6 more)

### Community 96 - ".GetAsync"
Cohesion: 0.18
Nodes (9): ControllerBase, microsoft_aspnetcore_mvc, HealthController, CancellationToken, Guid, HttpGet, IResult, Task (+1 more)

### Community 97 - "RunStage"
Cohesion: 0.14
Nodes (12): RunStage, Cancelled, Completed, Enriching, Failed, PausedForChallenge, Pending, Persisting (+4 more)

### Community 98 - "JobScrape.Application.Abstractions"
Cohesion: 0.13
Nodes (3): JobScrape.Infrastructure.Persistence, JobScrape.Application.Abstractions, JobScrape.Infrastructure.Persistence.Repositories

### Community 99 - "SourceCircuitBreakerTests"
Cohesion: 0.24
Nodes (5): ArgumentNullException, DateTimeOffset, DomainException, Fact, SourceCircuitBreakerTests

### Community 100 - "DataQualityIssues"
Cohesion: 0.22
Nodes (8): DataQualityIssues, DescriptionMissing, MissingCompany, MissingLocation, None, ParseLowConfidence, PostedDateUnknown, SalaryUnknown

### Community 101 - "useRenderLog"
Cohesion: 0.21
Nodes (15): react, CodeTag(), codeTone(), JsonBlock(), Module(), Stat(), fullScramble(), Scramble() (+7 more)

### Community 102 - "ADR-0014: Use Libraries as Designed; AOT Is Not a Constraint"
Cohesion: 0.25
Nodes (8): ADR-0014: Use Libraries as Designed; AOT Is Not a Constraint, Alternatives considered, Consequences, Context, Decision, Negative / trade-offs, Positive, References

### Community 103 - "ErrorInfo"
Cohesion: 0.23
Nodes (8): RunScrapeRequest, IReadOnlyDictionary, ErrorInfo, DateTimeOffset, Guid, Errors, ArgumentNullException, Fact

### Community 104 - "JobScrape.Domain.Access"
Cohesion: 0.12
Nodes (4): JobScrape.Domain.Tests.Access, JobScrape.Application.Features.Scraping, JobScrape.Domain.Access, JobScrape.Application.Tests.Scraping

### Community 105 - "DomainExceptionTests.cs"
Cohesion: 0.40
Nodes (3): JobScrape.Domain.Tests, Fact, DomainExceptionTests

### Community 106 - "graphify.js"
Cohesion: 0.47
Nodes (5): hasGraph(), location, IMPORTANT: keep the reminder string free of backticks and $(...) constructs., server(), setup()

### Community 107 - "xunit"
Cohesion: 0.12
Nodes (6): JobScrape.Application.Results, JobScrape.Application.Tests, JobScrape.Domain.Tests.Abstractions, JobScrape.Domain.Tests.Common, JobScrape.Domain.Tests.Sourcing, xunit

### Community 108 - "SearchPageParser"
Cohesion: 0.40
Nodes (6): HtmlParser, JSON-LD extraction, Visibility guards, Extraction priority (JSON-LD first), Honeypot and visibility guards, SearchPageParser

### Community 109 - "InitialCreate"
Cohesion: 0.10
Nodes (14): JobScrape.Infrastructure.Persistence.Migrations, Migration, MigrationBuilder, ModelSnapshot, DateOnly, Guid, DateOnly, Guid (+6 more)

### Community 110 - "JobPostingStatus"
Cohesion: 0.40
Nodes (4): JobPostingStatus, Active, Expired, Unknown

### Community 111 - ".AddJobScrapeOptions"
Cohesion: 0.10
Nodes (17): IOptions, IServiceCollection, IValidateOptions, OptionsRegistration, LoggingOptions, Directory, FileSizeLimitBytes, RetentionDays (+9 more)

### Community 112 - "JobScrapeDbContext"
Cohesion: 0.11
Nodes (16): DbContext, DbSet, IDesignTimeDbContextFactory, ModelConfigurationBuilder, DateTimeOffset, JobScrapeDbContext, BrowserProfiles, JobPostings (+8 more)

### Community 115 - ".EnrichAsync"
Cohesion: 0.26
Nodes (9): HashSet, RunScope, CancellationToken, DateTimeOffset, Func, IReadOnlyList, List, Task (+1 more)

### Community 116 - "TestDatabase"
Cohesion: 0.18
Nodes (10): AsyncServiceScope, IAsyncDisposable, ServiceProvider, ValueTask, TestDatabase, ConnectionString, Path, TestScope (+2 more)

### Community 117 - "RunScrapeHandler"
Cohesion: 0.34
Nodes (6): Exception, Guid, ILogger, LoggerMessage, TimeProvider, RunScrapeHandler

### Community 118 - "InMemoryStore"
Cohesion: 0.18
Nodes (11): InMemoryStore, BrowserProfiles, CancelAtSave, CancelOnSave, Circuits, FailAtSave, Postings, Quotas (+3 more)

### Community 119 - "BrowserProfileId"
Cohesion: 0.14
Nodes (9): CancellationToken, Task, CancellationToken, Task, ISessionManager, BrowserProfileId, Value, CancellationToken (+1 more)

### Community 120 - "app.tsx"
Cohesion: 0.22
Nodes (15): App(), checkHealth(), lookupProfile(), clockTime(), planeDown(), readHash(), useNow(), RulerTabs() (+7 more)

### Community 121 - "RunLimits"
Cohesion: 0.25
Nodes (7): TimeSpan, RunLimits, MaxDelay, MaxDetails, MaxJobs, MaxPages, MinDelay

### Community 122 - "SearchCriteria"
Cohesion: 0.22
Nodes (7): IReadOnlyList, SearchCriteria, ExperienceYears, Keywords, Locations, PostedWithinDays, RemoteOnly

### Community 123 - "SqlitePragmaInterceptor"
Cohesion: 0.28
Nodes (6): ConnectionEndEventData, DbConnection, DbConnectionInterceptor, CancellationToken, Task, SqlitePragmaInterceptor

### Community 124 - "CapturingLogger"
Cohesion: 0.16
Nodes (11): ILogger, LogEntry, Dictionary, EventId, Exception, Func, IDisposable, LogLevel (+3 more)

### Community 125 - "JobScrape.Infrastructure.Configuration"
Cohesion: 0.12
Nodes (7): JobScrape.Application.Features.Health, JobScrape.Infrastructure.Tests.Persistence, JobScrape.Infrastructure.Configuration, JobScrape.Infrastructure.Runtime, JobScrape.Domain, JobScrape.Infrastructure.Tests.Configuration, microsoft_extensions_time_testing

### Community 126 - "SourceSettingsCatalog"
Cohesion: 0.26
Nodes (8): ArgumentException, TimeSpan, SourceSettings, Dictionary, SourceSettingsCatalog, ArgumentNullException, Fact, SourceCatalogTests

### Community 127 - "ScriptedSource"
Cohesion: 0.20
Nodes (9): Queue, ScriptedSource, JobReference, ScriptedSource, Capabilities, DetailRequests, Key, Requests (+1 more)

### Community 128 - "RunScope"
Cohesion: 0.17
Nodes (12): RunScope, Adapter, ArtifactDirectory, Browser, Circuit, Context, Enrich, HitWall (+4 more)

### Community 129 - "ApiFactory"
Cohesion: 0.19
Nodes (10): IWebHostBuilder, Program, ApiFactory, DeleteLogsOnDispose, LogDirectory, ArgumentNullException, Fact, Task (+2 more)

### Community 130 - "SearchProfile"
Cohesion: 0.15
Nodes (13): DateTimeOffset, SearchProfile, CreatedAt, Criteria, Enabled, Id, Limits, Name (+5 more)

### Community 131 - "PageFingerprint"
Cohesion: 0.33
Nodes (6): PageFingerprint, FirstExternalId, ResultRange, DomainException, Fact, PageFingerprintTests

### Community 132 - "JobScrape.Api.Tests"
Cohesion: 0.20
Nodes (10): Microsoft.AspNetCore.Mvc.Testing, Serilog.Sinks.InMemory, JobScrape.Api.Tests, net10.0, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio (+2 more)

### Community 133 - "SourceRunContext"
Cohesion: 0.19
Nodes (8): CancellationToken, Task, CancellationToken, Task, SearchPageRequest, SourceRunContext, CancellationToken, Task

### Community 134 - "api.ts"
Cohesion: 0.18
Nodes (14): RFC-9457, ref_bun_test, ApiClient, ApiProblem, ApiResult, createApiClient(), FetchImpl, get() (+6 more)

### Community 135 - "ExternalJobId"
Cohesion: 0.19
Nodes (8): CancellationToken, Task, ExternalJobId, Value, CancellationToken, JobScrapeDbContext, Task, JobPostingRepository

### Community 136 - "JobScrape.Architecture.Tests"
Cohesion: 0.22
Nodes (9): NetArchTest.Rules, JobScrape.Architecture.Tests, net10.0, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3 (+1 more)

### Community 137 - "SourceKey"
Cohesion: 0.17
Nodes (12): IJobSource, Capabilities, Key, Dictionary, JobSourceRegistry, SourceCapabilities, StubSource, Capabilities (+4 more)

### Community 138 - "JobScrape.Application.Tests"
Cohesion: 0.22
Nodes (9): JobScrape.Application.Tests, net10.0, coverlet.collector, Microsoft.Extensions.TimeProvider.Testing, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3 (+1 more)

### Community 139 - "RunPausedForChallenge"
Cohesion: 0.38
Nodes (4): DateTimeOffset, RunPausedForChallenge, InlineData, Theory

### Community 140 - "SalaryPeriod"
Cohesion: 0.29
Nodes (6): SalaryPeriod, Day, Hour, Month, Week, Year

### Community 141 - "CancellationToken"
Cohesion: 0.17
Nodes (10): CancellationToken, DateOnly, List, Task, TimeSpan, RecordingArtifacts, Captured, RecordingDelay (+2 more)

### Community 142 - "RunScrapeOnSqliteTests"
Cohesion: 0.15
Nodes (14): IArtifactStore, CancellationToken, DateTimeOffset, Task, TimeSpan, MinRandom, NoArtifacts, NoDelay (+6 more)

### Community 143 - "JobScrape.Infrastructure.Tests"
Cohesion: 0.22
Nodes (9): JobScrape.Infrastructure.Tests, net10.0, coverlet.collector, Microsoft.Extensions.TimeProvider.Testing, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3 (+1 more)

### Community 144 - "WorkMode"
Cohesion: 0.33
Nodes (5): WorkMode, Hybrid, Onsite, Remote, Unknown

### Community 146 - "JobLocation"
Cohesion: 0.22
Nodes (8): LocationJson, JobLocation, City, Country, Raw, Region, WorkMode, LocationJson

### Community 147 - ".TwoRunsPersistDedupeAndChargeTheDailyQuota"
Cohesion: 0.13
Nodes (15): IBrowserProfileRepository, IScrapeRunRepository, ISourceCircuitBreakerRepository, ScrapeStores, IServiceCollection, PersistenceServiceCollectionExtensions, JobScrapeDbContext, BrowserProfileRepository (+7 more)

### Community 148 - "JobScrape.Domain.Tests"
Cohesion: 0.25
Nodes (8): JobScrape.Domain.Tests, net10.0, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3, Microsoft.NET.Sdk

### Community 149 - "ChallengeAction"
Cohesion: 0.13
Nodes (12): ChallengeAction, Continue, ManualPause, OpenCircuit, Pause, Retry, Stop, StopAndInspect (+4 more)

### Community 150 - "JobScrape.Infrastructure"
Cohesion: 0.29
Nodes (7): Microsoft.EntityFrameworkCore.Design, Microsoft.EntityFrameworkCore.Sqlite, Microsoft.Extensions.Options.DataAnnotations, JobScrape.Infrastructure, net10.0, Microsoft.Extensions.Logging.Abstractions, Microsoft.NET.Sdk

### Community 151 - "SourceCircuitBreaker"
Cohesion: 0.20
Nodes (11): DateTimeOffset, IReadOnlyList, List, TimeSpan, SourceCircuitBreaker, AllowsRun, ConsecutiveFailures, OpenUntil (+3 more)

### Community 152 - "SourcesOptions"
Cohesion: 0.17
Nodes (10): IValidateOptions, ComplianceOptions, AcknowledgeSourceRisk, AllowHighRiskSources, SourceSettingsMapper, Dictionary, SourcesOptions, Sources (+2 more)

### Community 153 - "ComplianceTier"
Cohesion: 0.40
Nodes (4): ComplianceTier, HighRisk, Restricted, Standard

### Community 155 - ".Parse"
Cohesion: 0.25
Nodes (5): DomainException, Fact, InlineData, Theory, SourceKeyTests

### Community 156 - "CanonicalUrl"
Cohesion: 0.19
Nodes (7): CanonicalUrl, Value, DomainException, Fact, InlineData, Theory, RehydrationTests

### Community 157 - "scripts"
Cohesion: 0.12
Nodes (16): scripts, api:types, audit, build, build:client, build:css, build:server, dev (+8 more)

### Community 158 - ".StartDay"
Cohesion: 0.31
Nodes (5): ArgumentNullException, DateTimeOffset, DomainException, Fact, SourceQuotaTests

### Community 159 - "OptionsTests"
Cohesion: 0.36
Nodes (4): Dictionary, Fact, ValidateOptionsResult, OptionsTests

### Community 160 - ".TimeProviderDelayHonorsCancellationAndRejectsNegative"
Cohesion: 0.19
Nodes (11): ArgumentOutOfRangeException, CancellationTokenSource, FakeTimeProvider, CancellationToken, Task, TimeProvider, TimeSpan, TimeProviderDelay (+3 more)

### Community 161 - "Naukri Playbook"
Cohesion: 0.14
Nodes (14): Akamai Bot Manager, Cloudflare Bot Management, Naukri Private Search API (jobapi/v3/search), Extraction Priority (JSON-LD first), PageFingerprint, JobSourceContractTests, Common Adapter Blueprint, URL Canonicalization (+6 more)

### Community 162 - "QuotaDecision"
Cohesion: 0.24
Nodes (6): QuotaDecision, QuotaKind, Details, Pages, Runs, DateTimeOffset

### Community 163 - "JobScrape.Api"
Cohesion: 0.15
Nodes (13): Microsoft.AspNetCore.OpenApi, Serilog.AspNetCore, Serilog.Enrichers.AssemblyName, Serilog.Enrichers.Demystifier, Serilog.Enrichers.Environment, Serilog.Enrichers.Process, Serilog.Enrichers.Span, Serilog.Enrichers.Thread (+5 more)

### Community 164 - "PersistenceTests"
Cohesion: 0.31
Nodes (5): IJobPostingRepository, CancellationToken, DateTimeOffset, PersistenceTests, Ct

### Community 165 - "ChallengeKind"
Cohesion: 0.15
Nodes (11): ChallengeKind, AccessDenied, Captcha, LoginRequired, None, RateLimited, SoftChallenge, SourceChanged (+3 more)

### Community 166 - "SourceOptions"
Cohesion: 0.15
Nodes (12): SourceOptions, BaseUrl, ChallengeCooldownMinutes, DailyDetailCap, DailyPageCap, DailyRunCap, Enabled, MaxDelayMs (+4 more)

### Community 167 - "CapturingLogger"
Cohesion: 0.21
Nodes (9): Level, EventId, Exception, Func, IDisposable, List, LogLevel, CapturingLogger (+1 more)

### Community 168 - "SearchProfileId"
Cohesion: 0.20
Nodes (6): CancellationToken, Task, SearchProfileId, Value, DateTimeOffset, SearchProfileCreated

### Community 169 - "GlobalExceptionHandler"
Cohesion: 0.24
Nodes (9): IExceptionHandler, PathString, CancellationToken, Exception, HttpContext, ILogger, LoggerMessage, ValueTask (+1 more)

### Community 170 - "tacmap.tsx"
Cohesion: 0.27
Nodes (7): @happy-dom/global-registrator, @testing-library/react, Compass(), cardinals(), gridLines(), TacMap(), samples

### Community 171 - "ChallengeDetector (anti-bot section 9.3)"
Cohesion: 0.22
Nodes (9): Browser hygiene standard, ChallengeDetector (anti-bot section 9.3), Detection surface matrix, Inert stealth init script finding, No fingerprint spoofing policy, Profile isolation per source, Public diagnostic sites (BotD, CreepJS), ChallengeClassifier (+1 more)

### Community 172 - ".AddJobScrapeLogging"
Cohesion: 0.29
Nodes (5): JobScrape.Api.Composition, IConfiguration, IOptions, IServiceCollection, LoggingRegistration

### Community 173 - "ResultOfTTests"
Cohesion: 0.48
Nodes (3): Fact, InvalidOperationException, ResultOfTTests

### Community 174 - "SourceQuotaRepository"
Cohesion: 0.38
Nodes (5): CancellationToken, DateOnly, JobScrapeDbContext, Task, SourceQuotaRepository

### Community 175 - "IDelay"
Cohesion: 0.33
Nodes (4): CancellationToken, Task, TimeSpan, IDelay

### Community 176 - "JobLens.Application"
Cohesion: 0.50
Nodes (5): JobLens.Application, JobLens CLI (composition root), JobLens.Infrastructure, ScrapeRun aggregate, SearchProfile aggregate

### Community 179 - "CircuitState"
Cohesion: 0.40
Nodes (4): CircuitState, Closed, HalfOpen, Open

### Community 180 - "SQLite Backup and Restore"
Cohesion: 0.50
Nodes (4): SQLite + EF Core 10, SQLite Backup and Restore, Monthly Upgrade Procedure, Working assumptions

### Community 182 - ".Configure"
Cohesion: 0.50
Nodes (3): DateTimeOffset, EntityTypeBuilder, List

## Knowledge Gaps
- **708 isolated node(s):** `$schema`, `type`, `url`, `oauth`, `Authorization` (+703 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1015 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **35 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `SourceKey` connect `SourceKey` to `SearchProfile`, `SourceRunContext`, `ExternalJobId`, `CancellationToken`, `RunScrapeOnSqliteTests`, `ScrapeHarness`, `SourceCircuitBreaker`, `ComplianceTier`, `.Parse`, `CanonicalUrl`, `.StartDay`, `PolicyTests`, `ChallengeKind`, `SearchProfileId`, `SearchProfileTests`, `JobSnapshot`, `.CreateAsync`, `.Create`, `SourceQuotaRepository`, `RunStats`, `JobPosting`, `.FindAsync`, `.FindAsync`, `ScrapeRun`, `BrowserProfile`, `SourceQuota`, `DomainConverters`, `SourceCircuitBreakerTests`, `ErrorInfo`, `.EnrichAsync`, `BrowserProfileId`, `SourceSettingsCatalog`, `ScriptedSource`?**
  _High betweenness centrality (0.102) - this node is a cross-community bridge._
- **Why does `ScrapeHarness` connect `ScrapeHarness` to `.TimeProviderDelayHonorsCancellationAndRejectsNegative`, `PolicyTests`, `SearchProfile`, `ErrorInfo`, `CancellationToken`, `InMemoryStore`, `BrowserProfile`, `CapturingLogger`, `JobScrape.Infrastructure.Configuration`, `SourceSettingsCatalog`, `ScriptedSource`?**
  _High betweenness centrality (0.062) - this node is a cross-community bridge._
- **Why does `ScrapeRun` connect `ScrapeRun` to `RunScope`, `RunStage`, `ChallengeKind`, `JobScrape.Domain.Common`, `SearchProfileId`, `SourceKey`, `CancellationToken`, `ScrapeRunTests`, `JobScrapeDbContext`, `.GetAsync`, `RunStats`, `AggregateRoot`, `InMemoryStore`, `ScrapeHarness`, `.OnModelCreating`, `RunLimits`, `.GetAsync`?**
  _High betweenness centrality (0.052) - this node is a cross-community bridge._
- **What connects `$schema`, `type`, `url` to the rest of the system?**
  _708 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `SearchProfilesEndpointTests.cs` be split into smaller, more focused modules?**
  _Cohesion score 0.11397849462365592 - nodes in this community are weakly interconnected._
- **Should `2. Structured logging with Serilog` be split into smaller, more focused modules?**
  _Cohesion score 0.044444444444444446 - nodes in this community are weakly interconnected._
- **Should `package.json` be split into smaller, more focused modules?**
  _Cohesion score 0.07407407407407407 - nodes in this community are weakly interconnected._