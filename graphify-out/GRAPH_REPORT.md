# Graph Report - PlaywrightDotNet  (2026-11-01)

## Corpus Check
- 19 files · ~81,109 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 945 nodes · 1397 edges · 87 communities (54 shown, 33 thin omitted)
- Extraction: 89% EXTRACTED · 11% INFERRED · 0% AMBIGUOUS · INFERRED: 157 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Ops Deck Pages
- SearchProfiles Tests
- Runtime Hardening
- Frontend Package
- Adapter Blueprint
- Documentation Index
- Application Patterns
- Delivery Gameplan
- Result Contracts
- Architecture & Quality Gates
- TS Config
- BFF Server
- Use-Case Handlers
- Web Cockpit Design
- SearchProfile Handler
- API Controllers
- Browser Sessions
- Result Factory Hub
- Library Policy
- MCP Servers
- Error Taxonomy
- Conventional Commits
- Source Contracts
- Endpoint Tests
- Compliance Non-Negotiables
- CLI & Architecture
- Handler Tests
- Compliance & Risk
- Domain Aggregates
- Challenge Policy
- Compliance Policy
- Launch Profiles
- Frontend README
- Domain Models
- Test Pyramid
- Phase 0 Decisions
- No CQRS ADR
- Serilog Polly ADR
- AOT Policy ADR
- JobScrape Naming ADR
- API Test Projects
- Application Test Projects
- Architecture Test Projects
- Result Errors
- Domain Test Projects
- Validation Errors
- BFF Runtime
- Result ErrorInfo Mapping
- Result Contracts
- Layer Rules Tests
- Domain Exceptions
- API Projects
- Application Projects
- Infrastructure Projects
- Design Documents
- Persistence Unit of Work
- Retry & Page Fetching
- SDK & Package Pinning
- Definition of Done
- Domain Projects
- AGENTS Rules
- Graphify Workflow
- Detection Surface
- Browser Profile Credential
- Result Pattern Mapping
- Specification Queries
- Scope & YAGNI
- CI Canary Policy
- HTTP Error Contract
- Deck Router
- Client Entry
- Commit Policy
- AngleSharp Parsing
- Excel Export
- Pre-Phase Spikes
- CLI Framework
- Time Provider Injection
- Gameplan Document
- Testing Strategy Document
- MTP Coverage Extension
- Async Resource Management
- Log Redaction
- Cockpit Favicon

## God Nodes (most connected - your core abstractions)
1. `compilerOptions` - 20 edges
2. `Web cockpit (v2 control plane)` - 18 edges
3. `App()` - 17 edges
4. `scripts` - 16 edges
5. `RunScrapeHandler` - 16 edges
6. `SearchProfileResponse` - 13 edges
7. `ErrorKind` - 13 edges
8. `Phase 0 open decisions (D1-D12 with recommended defaults)` - 13 edges
9. `JobLens` - 12 edges
10. `InMemorySearchProfileStore` - 11 edges

## Surprising Connections (you probably didn't know these)
- `UI design in src/frontend` --conceptually_related_to--> `JobScrape Cockpit control plane`  [INFERRED]
  AGENTS.md → src/frontend/README.md
- `JobLens` --conceptually_related_to--> `Clean Architecture dependency rule`  [INFERRED]
  README.md → docs/02-architecture.md
- `RunScrapeHandler` --references--> `QuotaPolicy`  [INFERRED]
  docs/02-architecture.md → docs/03-domain-model.md
- `SearchPageResult` --references--> `PageFingerprint`  [INFERRED]
  docs/02-architecture.md → docs/03-domain-model.md
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
- **Deterministic C# formatting workflow** — docs_04_solution_and_tooling_code_conventions, docs_10_adr_0016_csharp_style_csharpier_style_guides, docs_10_adr_0016_csharp_style_csharpier_csharpier_formatting [INFERRED]
- **xml docs enforcement flow** — docs_04_solution_and_tooling_code_conventions, docs_10_adr_0017_xml_documentation_rule_xml_doc_build_rule, docs_10_adr_readme_adr_index [INFERRED]
- **** — docs_12_web_cockpit_control_plane, docs_12_web_cockpit_bun_bff, docs_12_web_cockpit_dotnet_data_plane [INFERRED]
- **Ops deck request flow** —  [INFERRED]

## Communities (87 total, 33 thin omitted)

### Community 0 - "Ops Deck Pages"
Cohesion: 0.08
Nodes (50): RFC-9457, react, App(), checkHealth(), lookupProfile(), clockTime(), planeDown(), readHash() (+42 more)

### Community 1 - "SearchProfiles Tests"
Cohesion: 0.07
Nodes (11): JobScrape.Application.Results, JobScrape.Api.Tests, JobScrape.Infrastructure.SearchProfiles, JobScrape.Api.Http, JobScrape.Api.Controllers, JobScrape.Application.Tests, JobScrape.Domain.Tests, JobScrape.Architecture.Tests (+3 more)

### Community 2 - "Runtime Hardening"
Cohesion: 0.04
Nodes (45): 1. Baseline decisions, 2.1 Layering, 2.2 Pipeline, 2.3 Bootstrap, 2.4 Enrichers, 2.5 Correlation and scopes, 2.6 Message templates and performance, 2.7 Redaction (+37 more)

### Community 3 - "Frontend Package"
Cohesion: 0.05
Nodes (44): happy-dom, @happy-dom/global-registrator, react-dom, tailwindcss, @tailwindcss/cli, @testing-library/react, @types/bun, @types/react (+36 more)

### Community 4 - "Adapter Blueprint"
Cohesion: 0.06
Nodes (39): Naukri Private Search API (jobapi/v3/search), HtmlParser, JobSourceContractTests, JSON-LD extraction, Visibility guards, Browser hygiene standard, Challenge kinds taxonomy, ChallengeAssessment (kind, evidence, confidence) (+31 more)

### Community 5 - "Documentation Index"
Cohesion: 0.10
Nodes (21): .NET 10 LTS, CI Outline restore build test E2E coverage, Code Conventions and csharpier workflow, Solution and Tooling, Options binding and startup validation, Solution and Tooling, ADR-0014: Use Libraries as Designed; AOT Is Not a Constraint, Alternatives considered (+13 more)

### Community 6 - "Application Patterns"
Cohesion: 0.05
Nodes (39): 1. Pattern map, 2.1 Decision, 2.2 Banned, 2.3 Replacements, 2.4 Why, 2.5 Allowed read/write separation, 2.6 Enforcement, 2. No CQRS, no MediatR (+31 more)

### Community 7 - "Delivery Gameplan"
Cohesion: 0.06
Nodes (34): ADR-0010 React + Bun cockpit, Modular Monolith with Clean Architecture and inward dependency rule, v2 Web Cockpit, Decision gates G0–G5, First 15 commits, Phase 0 — Foundation, Phase 1 — Domain + Application, Phase 2 — Browser + Session (+26 more)

### Community 8 - "Result Contracts"
Cohesion: 0.12
Nodes (5): ErrorInfoTests, GetHealthHandlerTests, ResultOfTTests, ResultTests, DomainExceptionTests

### Community 9 - "Architecture & Quality Gates"
Cohesion: 0.09
Nodes (21): v1 Scope Boundary, v1 Success Criteria, SQLite + EF Core 10, NetArchTest architecture tests, JobLens.Application, JobLens CLI (composition root), JobLens.Domain, JobLens.Infrastructure (+13 more)

### Community 10 - "TS Config"
Cohesion: 0.09
Nodes (21): compilerOptions, allowImportingTsExtensions, allowJs, isolatedModules, jsx, lib, module, moduleDetection (+13 more)

### Community 11 - "BFF Server"
Cohesion: 0.15
Nodes (12): RFC-9110, CockpitConfig, parseApiUrl(), parseConfig(), parsePort(), apiProxy(), createHandler(), buildTargetUrl() (+4 more)

### Community 12 - "Use-Case Handlers"
Cohesion: 0.16
Nodes (15): ArtifactStore, ChallengeAssessment, ChallengeDetector, ChallengeKind, CompliancePolicy, IJobSource, JobSnapshot, JobSourceRegistry (+7 more)

### Community 13 - "Web Cockpit Design"
Cohesion: 0.19
Nodes (16): React + Bun web cockpit (v2.0), shadcn/ui copy-in components, HTTP/JSON commands + Server-Sent Events transport, Backend-first scaffold, Explicit Main + block namespaces, Architecture Decision Record (ADR), Bun native package manager (bun install/bun ci, bun.lock, minimumReleaseAge), Web cockpit (v2 control plane) (+8 more)

### Community 14 - "SearchProfile Handler"
Cohesion: 0.29
Nodes (3): InMemorySearchProfileStoreTests, SearchProfileResponse, InMemorySearchProfileStore

### Community 15 - "API Controllers"
Cohesion: 0.15
Nodes (6): JobScrape.Application.Features.Health, HealthController, SearchProfilesController, HealthPayload, GetHealthHandler, GetHealthResponse

### Community 16 - "Browser Sessions"
Cohesion: 0.17
Nodes (14): Playwright for .NET, Public Browser Diagnostics, WebKit Is Not Safari, xUnit.net v3, BrowserSessionFactory, ChallengeDetector and ChallengeAssessment, ISessionManager, BrowserProfile (+6 more)

### Community 17 - "Result Factory Hub"
Cohesion: 0.28
Nodes (3): ErrorMapper, ErrorMapperTests, ErrorInfo

### Community 18 - "Library Policy"
Cohesion: 0.14
Nodes (14): 1. Rules, 2. Library-specific guidance, 3. What we still enforce, 4. Reviewing a new library, 5. Related decisions, 6. References, ClosedXML, EF Core and Playwright (+6 more)

### Community 19 - "MCP Servers"
Cohesion: 0.14
Nodes (13): headers, oauth, type, url, Authorization, mcp, servers, $schema (+5 more)

### Community 20 - "Error Taxonomy"
Cohesion: 0.14
Nodes (13): ErrorKind, Challenge, Conflict, CoolingDown, Forbidden, NotFound, QuotaExceeded, RateLimited (+5 more)

### Community 21 - "Conventional Commits"
Cohesion: 0.15
Nodes (13): 1.1 Header, 1.2 Body, 1.3 Footers, 1.4 Breaking changes, 1. Format, 2.1 Scopes, 2. Types used in this repository, 3.1 Examples (+5 more)

### Community 22 - "Source Contracts"
Cohesion: 0.17
Nodes (11): Akamai Bot Manager, Cloudflare Bot Management, PageFingerprint, JobSourceContractTests, Common Adapter Blueprint, URL Canonicalization, Indeed India Playbook, Native Next Pagination (Naukri) (+3 more)

### Community 23 - "Endpoint Tests"
Cohesion: 0.23
Nodes (3): Program, HealthEndpointTests, SearchProfilesEndpointTests

### Community 24 - "Compliance Non-Negotiables"
Cohesion: 0.24
Nodes (3): Anti-ban by being a good citizen, HumanPacing, Pacing and quotas

### Community 25 - "CLI & Architecture"
Cohesion: 0.18
Nodes (10): .NET 10 LTS, global.json SDK pin (10.0.403, rollForward latestFeature), Clean Architecture layers (Domain, Application, Infrastructure, Cli), Focused (tactical) DDD for Sourcing and Access, Modular monolith, EF Core 10.0.14, SQLite as source of truth, WAL single-writer configuration (+2 more)

### Community 26 - "Handler Tests"
Cohesion: 0.33
Nodes (5): GetSearchProfileHandler, ISearchProfileStore, GetSearchProfileHandlerTests, StubStore, WasCalled

### Community 27 - "Compliance & Risk"
Cohesion: 0.22
Nodes (10): CircuitBreaker (per source), Exponential backoff policy, Challenge and block metrics, Per-source circuit breaker, Cooldown policy, doctor command, Incident runbook, Review cadence (+2 more)

### Community 28 - "Domain Aggregates"
Cohesion: 0.20
Nodes (9): JobLens Bounded Contexts, PolitenessPolicy, QuotaPolicy, RunEvent, RunLimits, ScrapeRun, SearchCriteria, SearchProfile (+1 more)

### Community 29 - "Challenge Policy"
Cohesion: 0.22
Nodes (10): Per-Source Circuit Breaker, JobLens CLI Surface, joblens doctor, CLI Exit Codes, Cooldown Ladder, Anti-Bot Metrics and Alerts, Retention Policy, OS Scheduling (cron/launchd/Task Scheduler) (+2 more)

### Community 30 - "Compliance Policy"
Cohesion: 0.27
Nodes (8): CompliancePolicy, ComplianceTier, CompliancePolicy gate, Fair use checklist, robots.txt findings (2026-10-29), Source risk tiers and gates, LinkedIn consent gate, Operator Security Checklist

### Community 31 - "Launch Profiles"
Cohesion: 0.20
Nodes (9): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, profiles, http (+1 more)

### Community 32 - "Frontend README"
Cohesion: 0.25
Nodes (9): UI design in src/frontend, ui-skills catalog router, BFF API proxy, .NET data plane API localhost 5207, Bun React TypeScript Tailwind stack, JobScrape Cockpit control plane, globals.css stylesheet link, main.tsx module entry (+1 more)

### Community 33 - "Domain Models"
Cohesion: 0.31
Nodes (9): Idempotent upsert by (SourceKey, ExternalJobId), IJobPostingRepository, JobNormalizer, JobPosting aggregate, ContentHash, DeduplicationPolicy, JobPosting, JobSkill (+1 more)

### Community 34 - "Test Pyramid"
Cohesion: 0.25
Nodes (8): xUnit.net v3, Playwright for .NET, Manual/nightly live canaries (never in CI), Local fixture server for Playwright E2E, Risk-based coverage gates (Domain 95/90, Application 90/85, overall 85/80), FakeTimeProvider namespace rename, Native MTP test runner, Bundled Chromium default browser (D9)

### Community 35 - "Phase 0 Decisions"
Cohesion: 0.31
Nodes (9): Per-source compliance tiers and consent gates, CSV + Excel export (JSON optional, Postgres later), Data retention 30/90/180 days (D12), Full posting field set (D4), Indeed opt-in with conservative caps (D3), JobLens working project name (D1), LinkedIn opt-in high-risk, default disabled (D2), Phase 0 open decisions (D1-D12 with recommended defaults) (+1 more)

### Community 36 - "No CQRS ADR"
Cohesion: 0.22
Nodes (9): ADR-0011: Explicit Use-Case Handlers, No CQRS or MediatR, Alternatives considered, Consequences, Context, Decision, Follow-ups, Negative / trade-offs, Positive (+1 more)

### Community 37 - "Serilog Polly ADR"
Cohesion: 0.22
Nodes (9): ADR-0012: Serilog and Polly Runtime Hardening, Alternatives considered, Consequences, Context, Decision, Follow-ups, Negative / trade-offs, Positive (+1 more)

### Community 38 - "AOT Policy ADR"
Cohesion: 0.22
Nodes (9): ADR-0013: AOT-Friendly Code, No NativeAOT Release, Alternatives considered, Consequences, Context, Decision, Follow-ups, Negative / trade-offs, Positive (+1 more)

### Community 39 - "JobScrape Naming ADR"
Cohesion: 0.22
Nodes (9): ADR-0015: JobScrape solution naming, backend-first scaffold, native MTP tests, Alternatives considered, Consequences, Context, Decision, Follow-ups, Negative / trade-offs, Positive (+1 more)

### Community 40 - "API Test Projects"
Cohesion: 0.22
Nodes (8): Microsoft.AspNetCore.Mvc.Testing, net10.0, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3, Microsoft.NET.Sdk

### Community 41 - "Application Test Projects"
Cohesion: 0.22
Nodes (8): Microsoft.Extensions.TimeProvider.Testing, net10.0, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3, Microsoft.NET.Sdk

### Community 42 - "Architecture Test Projects"
Cohesion: 0.22
Nodes (8): NetArchTest.Rules, net10.0, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3, Microsoft.NET.Sdk

### Community 43 - "Result Errors"
Cohesion: 0.25
Nodes (4): Result, Error, IsFailure, IsSuccess

### Community 44 - "Domain Test Projects"
Cohesion: 0.25
Nodes (7): net10.0, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3, Microsoft.NET.Sdk

### Community 45 - "Validation Errors"
Cohesion: 0.29
Nodes (3): Optional Naukri API fast path (jobapi/v3/search with RSA nkparam), Normal network egress, no rotation (D8), Challenge handoff to host headed browser

### Community 46 - "BFF Runtime"
Cohesion: 0.29
Nodes (7): Same-origin Bun BFF, Cockpit auth: loopback bind, HttpOnly cookie, CSRF double-submit, BFF bearer token, Bun BFF runtime (Bun.serve, /api/v1 proxy, SSE proxy, static hosting), Web Cockpit Control Plane, CSP and security headers, .NET API Data Plane, SSE Run Event Stream

### Community 48 - "Result Contracts"
Cohesion: 0.33
Nodes (5): Result, Error, IsFailure, IsSuccess, Value

### Community 51 - "API Projects"
Cohesion: 0.50
Nodes (3): Microsoft.AspNetCore.OpenApi, Microsoft.NET.Sdk.Web, net10.0

### Community 52 - "Application Projects"
Cohesion: 0.50
Nodes (3): net10.0, Microsoft.Extensions.Logging.Abstractions, Microsoft.NET.Sdk

### Community 53 - "Infrastructure Projects"
Cohesion: 0.50
Nodes (3): net10.0, Microsoft.Extensions.Logging.Abstractions, Microsoft.NET.Sdk

### Community 54 - "Design Documents"
Cohesion: 1.00
Nodes (3): Architecture Document, Application Patterns Document, Runtime Hardening Document

### Community 57 - "SDK & Package Pinning"
Cohesion: 0.67
Nodes (3): Central Package Management, Layered projects and dependency graph, global.json SDK Pin

### Community 58 - "Definition of Done"
Cohesion: 0.67
Nodes (3): Backlog epics E0–E9, Definition of Ready / Done, Coverage and quality metrics

## Knowledge Gaps
- **177 isolated node(s):** `Challenge`, `Conflict`, `CoolingDown`, `Forbidden`, `NotFound` (+172 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 417 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **33 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `IJobSource` connect `Use-Case Handlers` to `Architecture & Quality Gates`, `Adapter Blueprint`, `Source Contracts`, `Compliance Policy`?**
  _High betweenness centrality (0.085) - this node is a cross-community bridge._
- **What connects `Challenge`, `Conflict`, `CoolingDown` to the rest of the system?**
  _177 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Ops Deck Pages` be split into smaller, more focused modules?**
  _Cohesion score 0.08397271952259165 - nodes in this community are weakly interconnected._
- **Why does `JobLens` connect `Architecture & Quality Gates` to `Documentation Index`?**
  _High betweenness centrality (0.072) - this node is a cross-community bridge._
- **Should `SearchProfiles Tests` be split into smaller, more focused modules?**
  _Cohesion score 0.06859903381642513 - nodes in this community are weakly interconnected._
- **Why does `NetArchTest architecture tests` connect `Architecture & Quality Gates` to `Use-Case Handlers`?**
  _High betweenness centrality (0.070) - this node is a cross-community bridge._
- **Should `Runtime Hardening` be split into smaller, more focused modules?**
  _Cohesion score 0.044444444444444446 - nodes in this community are weakly interconnected._