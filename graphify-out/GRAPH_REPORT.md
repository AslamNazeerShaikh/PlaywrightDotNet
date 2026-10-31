# Graph Report - PlaywrightDotNet  (2026-10-31)

## Corpus Check
- 14 files · ~72,789 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 768 nodes · 1088 edges · 71 communities (43 shown, 28 thin omitted)
- Extraction: 86% EXTRACTED · 14% INFERRED · 0% AMBIGUOUS · INFERRED: 154 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- SearchProfile Handler
- Result Contracts
- Runtime Hardening
- SearchProfiles Tests
- Delivery Gameplan
- Application Patterns
- Adapter Blueprint
- Browser Sessions
- Documentation Index
- Web Cockpit
- Architecture & Quality Gates
- Library Policy
- Error Taxonomy
- Conventional Commits
- Compliance Non-Negotiables
- Source Contracts
- Compliance & Risk
- C# Style & Format
- Use-Case Handlers
- Domain Aggregates
- GitHub MCP Config
- Launch Profiles
- Layer Rules Tests
- Posting Deduplication
- No CQRS ADR
- Serilog Polly ADR
- AOT Policy ADR
- JobScrape Naming ADR
- API Test Projects
- Application Test Projects
- Architecture Test Projects
- Result Factory Hub
- Challenge Detection
- Operations Runbook
- Libraries As Designed ADR
- Domain Test Projects
- Result ErrorInfo Mapping
- Data Retention
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
- Detection Surface
- Browser Profile Credential
- Result Pattern Mapping
- Specification Queries
- Scope & YAGNI
- CI Canary Policy
- HTTP Error Contract
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
- Frontend Scaffold

## God Nodes (most connected - your core abstractions)
1. `Web cockpit (v2 control plane)` - 18 edges
2. `RunScrapeHandler` - 16 edges
3. `Phase 0 open decisions (D1-D12 with recommended defaults)` - 14 edges
4. `ErrorKind` - 13 edges
5. `SearchProfileResponse` - 13 edges
6. `JobLens` - 12 edges
7. `Solution and Tooling` - 11 edges
8. `IJobSource` - 11 edges
9. `ErrorInfo` - 11 edges
10. `InMemorySearchProfileStore` - 11 edges

## Surprising Connections (you probably didn't know these)
- `v1 Success Criteria` --references--> `JobLens`  [EXTRACTED]
  docs/00-executive-summary.md → README.md
- `JobLens` --conceptually_related_to--> `Clean Architecture dependency rule`  [INFERRED]
  README.md → docs/02-architecture.md
- `v1 Scope Boundary` --references--> `JobLens`  [EXTRACTED]
  docs/00-executive-summary.md → README.md
- `React 19 plus Bun local web cockpit` --conceptually_related_to--> `JobLens v1 Phase Plan`  [INFERRED]
  src/frontend/README.md → docs/05-gameplan.md
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
- **Deterministic C# formatting workflow** — docs_04_solution_and_tooling_code_conventions, docs_10_adr_0016_csharp_style_csharpier_style_guides, docs_10_adr_0016_csharp_style_csharpier_csharpier_formatting [INFERRED]
- **xml docs enforcement flow** — docs_04_solution_and_tooling_code_conventions, docs_10_adr_0017_xml_documentation_rule_xml_doc_build_rule, docs_10_adr_readme_adr_index [INFERRED]

## Communities (71 total, 28 thin omitted)

### Community 0 - "SearchProfile Handler"
Cohesion: 0.08
Nodes (17): JobScrape.Application.Features.Health, HealthController, SearchProfilesController, Program, HealthEndpointTests, HealthPayload, InMemorySearchProfileStoreTests, SearchProfilesEndpointTests (+9 more)

### Community 1 - "Result Contracts"
Cohesion: 0.07
Nodes (9): ErrorMapper, ErrorMapperTests, ErrorInfo, ErrorInfoTests, ErrorsTests, GetHealthHandlerTests, ResultOfTTests, ResultTests (+1 more)

### Community 2 - "Runtime Hardening"
Cohesion: 0.04
Nodes (45): 1. Baseline decisions, 2.1 Layering, 2.2 Pipeline, 2.3 Bootstrap, 2.4 Enrichers, 2.5 Correlation and scopes, 2.6 Message templates and performance, 2.7 Redaction (+37 more)

### Community 3 - "SearchProfiles Tests"
Cohesion: 0.08
Nodes (10): JobScrape.Application.Results, JobScrape.Api.Tests, JobScrape.Infrastructure.SearchProfiles, JobScrape.Api.Http, JobScrape.Api.Controllers, JobScrape.Application.Tests, JobScrape.Domain.Tests, JobScrape.Api (+2 more)

### Community 4 - "Delivery Gameplan"
Cohesion: 0.06
Nodes (35): ADR-0010 React + Bun cockpit, Modular Monolith with Clean Architecture and inward dependency rule, v2 Web Cockpit, Decision gates G0–G5, First 15 commits, Phase 0 — Foundation, Phase 1 — Domain + Application, Phase 2 — Browser + Session (+27 more)

### Community 5 - "Application Patterns"
Cohesion: 0.05
Nodes (39): 1. Pattern map, 2.1 Decision, 2.2 Banned, 2.3 Replacements, 2.4 Why, 2.5 Allowed read/write separation, 2.6 Enforcement, 2. No CQRS, no MediatR (+31 more)

### Community 6 - "Adapter Blueprint"
Cohesion: 0.06
Nodes (36): Naukri Private Search API (jobapi/v3/search), HtmlParser, JobSourceContractTests, JSON-LD extraction, Visibility guards, Challenge kinds taxonomy, ChallengeAssessment (kind, evidence, confidence), ChallengeDetector (anti-bot section 9.3) (+28 more)

### Community 7 - "Browser Sessions"
Cohesion: 0.07
Nodes (32): v1 Success Criteria, Playwright for .NET, Public Browser Diagnostics, SQLite + EF Core 10, WebKit Is Not Safari, xUnit.net v3, BrowserSessionFactory, ChallengeDetector and ChallengeAssessment (+24 more)

### Community 8 - "Documentation Index"
Cohesion: 0.17
Nodes (13): .NET 10 LTS, Solution and Tooling, Per-source compliance tiers and consent gates, CSV + Excel export (JSON optional, Postgres later), Data retention 30/90/180 days (D12), Full posting field set (D4), Indeed opt-in with conservative caps (D3), JobLens working project name (D1) (+5 more)

### Community 9 - "Web Cockpit"
Cohesion: 0.09
Nodes (30): .NET 10 LTS, global.json SDK pin (10.0.403, rollForward latestFeature), Clean Architecture layers (Domain, Application, Infrastructure, Cli), Focused (tactical) DDD for Sourcing and Access, Modular monolith, EF Core 10.0.14, SQLite as source of truth, WAL single-writer configuration (+22 more)

### Community 10 - "Architecture & Quality Gates"
Cohesion: 0.08
Nodes (22): NetArchTest architecture tests, JobLens.Application, JobLens CLI (composition root), JobLens.Domain, JobLens.Infrastructure, ScrapeRun aggregate, SearchProfile aggregate, Package Matrix central package management (+14 more)

### Community 11 - "Library Policy"
Cohesion: 0.14
Nodes (14): 1. Rules, 2. Library-specific guidance, 3. What we still enforce, 4. Reviewing a new library, 5. Related decisions, 6. References, ClosedXML, EF Core and Playwright (+6 more)

### Community 12 - "Error Taxonomy"
Cohesion: 0.14
Nodes (13): ErrorKind, Challenge, Conflict, CoolingDown, Forbidden, NotFound, QuotaExceeded, RateLimited (+5 more)

### Community 13 - "Conventional Commits"
Cohesion: 0.15
Nodes (13): 1.1 Header, 1.2 Body, 1.3 Footers, 1.4 Breaking changes, 1. Format, 2.1 Scopes, 2. Types used in this repository, 3.1 Examples (+5 more)

### Community 14 - "Compliance Non-Negotiables"
Cohesion: 0.21
Nodes (4): v1 Scope Boundary, Anti-ban by being a good citizen, Browser hygiene standard, Profile isolation per source

### Community 15 - "Source Contracts"
Cohesion: 0.17
Nodes (11): Akamai Bot Manager, Cloudflare Bot Management, PageFingerprint, JobSourceContractTests, Common Adapter Blueprint, URL Canonicalization, Indeed India Playbook, Native Next Pagination (Naukri) (+3 more)

### Community 16 - "Compliance & Risk"
Cohesion: 0.24
Nodes (10): CompliancePolicy, ComplianceTier, CompliancePolicy gate, Fair use checklist, HumanPacing, Pacing and quotas, robots.txt findings (2026-10-29), Source risk tiers and gates (+2 more)

### Community 17 - "C# Style & Format"
Cohesion: 0.20
Nodes (11): CI Outline restore build test E2E coverage, Code Conventions and csharpier workflow, Options binding and startup validation, Solution and Tooling, csharpier formatting ownership, ADR-0016 C# style guides and csharpier formatting, C# style guides ktaranov plus Google, ADR-0017 XML documentation as global build rule (+3 more)

### Community 18 - "Use-Case Handlers"
Cohesion: 0.24
Nodes (9): ArtifactStore, CompliancePolicy, IJobSource, JobSnapshot, JobSourceRegistry, Page fingerprint, RunScrapeHandler, SearchPageResult (+1 more)

### Community 19 - "Domain Aggregates"
Cohesion: 0.20
Nodes (9): JobLens Bounded Contexts, PolitenessPolicy, QuotaPolicy, RunEvent, RunLimits, ScrapeRun, SearchCriteria, SearchProfile (+1 more)

### Community 20 - "GitHub MCP Config"
Cohesion: 0.20
Nodes (9): headers, oauth, type, url, Authorization, mcp, servers, $schema (+1 more)

### Community 21 - "Launch Profiles"
Cohesion: 0.20
Nodes (9): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, profiles, http (+1 more)

### Community 23 - "Posting Deduplication"
Cohesion: 0.31
Nodes (9): Idempotent upsert by (SourceKey, ExternalJobId), IJobPostingRepository, JobNormalizer, JobPosting aggregate, ContentHash, DeduplicationPolicy, JobPosting, JobSkill (+1 more)

### Community 24 - "No CQRS ADR"
Cohesion: 0.22
Nodes (9): ADR-0011: Explicit Use-Case Handlers, No CQRS or MediatR, Alternatives considered, Consequences, Context, Decision, Follow-ups, Negative / trade-offs, Positive (+1 more)

### Community 25 - "Serilog Polly ADR"
Cohesion: 0.22
Nodes (9): ADR-0012: Serilog and Polly Runtime Hardening, Alternatives considered, Consequences, Context, Decision, Follow-ups, Negative / trade-offs, Positive (+1 more)

### Community 26 - "AOT Policy ADR"
Cohesion: 0.22
Nodes (9): ADR-0013: AOT-Friendly Code, No NativeAOT Release, Alternatives considered, Consequences, Context, Decision, Follow-ups, Negative / trade-offs, Positive (+1 more)

### Community 27 - "JobScrape Naming ADR"
Cohesion: 0.22
Nodes (9): ADR-0015: JobScrape solution naming, backend-first scaffold, native MTP tests, Alternatives considered, Consequences, Context, Decision, Follow-ups, Negative / trade-offs, Positive (+1 more)

### Community 28 - "API Test Projects"
Cohesion: 0.22
Nodes (8): Microsoft.AspNetCore.Mvc.Testing, net10.0, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3, Microsoft.NET.Sdk

### Community 29 - "Application Test Projects"
Cohesion: 0.22
Nodes (8): Microsoft.Extensions.TimeProvider.Testing, net10.0, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3, Microsoft.NET.Sdk

### Community 30 - "Architecture Test Projects"
Cohesion: 0.22
Nodes (8): NetArchTest.Rules, net10.0, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3, Microsoft.NET.Sdk

### Community 31 - "Result Factory Hub"
Cohesion: 0.22
Nodes (4): Result, Error, IsFailure, IsSuccess

### Community 32 - "Challenge Detection"
Cohesion: 0.32
Nodes (6): ChallengeAssessment, ChallengeDetector, ChallengeKind, ChallengeKind, ChallengePolicy, RetryPolicy

### Community 33 - "Operations Runbook"
Cohesion: 0.29
Nodes (8): Per-Source Circuit Breaker, JobLens CLI Surface, joblens doctor, CLI Exit Codes, Cooldown Ladder, Anti-Bot Metrics and Alerts, OS Scheduling (cron/launchd/Task Scheduler), Troubleshooting Matrix

### Community 34 - "Libraries As Designed ADR"
Cohesion: 0.25
Nodes (8): ADR-0014: Use Libraries as Designed; AOT Is Not a Constraint, Alternatives considered, Consequences, Context, Decision, Negative / trade-offs, Positive, References

### Community 35 - "Domain Test Projects"
Cohesion: 0.25
Nodes (7): net10.0, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3, Microsoft.NET.Sdk

### Community 36 - "Result ErrorInfo Mapping"
Cohesion: 0.33
Nodes (5): Result, Error, IsFailure, IsSuccess, Value

### Community 37 - "Data Retention"
Cohesion: 0.40
Nodes (3): Retention Policy, Operator Security Checklist, Artifacts Storage Layout

### Community 39 - "API Projects"
Cohesion: 0.50
Nodes (3): Microsoft.AspNetCore.OpenApi, Microsoft.NET.Sdk.Web, net10.0

### Community 40 - "Application Projects"
Cohesion: 0.50
Nodes (3): net10.0, Microsoft.Extensions.Logging.Abstractions, Microsoft.NET.Sdk

### Community 41 - "Infrastructure Projects"
Cohesion: 0.50
Nodes (3): net10.0, Microsoft.Extensions.Logging.Abstractions, Microsoft.NET.Sdk

### Community 42 - "Design Documents"
Cohesion: 1.00
Nodes (3): Architecture Document, Application Patterns Document, Runtime Hardening Document

### Community 45 - "SDK & Package Pinning"
Cohesion: 0.67
Nodes (3): Central Package Management, Layered projects and dependency graph, global.json SDK Pin

### Community 46 - "Definition of Done"
Cohesion: 0.67
Nodes (3): Backlog epics E0–E9, Definition of Ready / Done, Coverage and quality metrics

## Knowledge Gaps
- **165 isolated node(s):** `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.AspNetCore.OpenApi`, `coverlet.collector`, `Microsoft.NET.Test.Sdk`, `Microsoft.Testing.Extensions.CodeCoverage` (+160 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 348 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **28 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `JobLens` connect `Documentation Index` to `Architecture & Quality Gates`, `Compliance Non-Negotiables`, `Browser Sessions`?**
  _High betweenness centrality (0.131) - this node is a cross-community bridge._
- **Are the 4 inferred relationships involving `RunScrapeHandler` (e.g. with `ChallengePolicy` and `Per-Source Circuit Breaker`) actually correct?**
  _`RunScrapeHandler` has 4 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.AspNetCore.OpenApi`, `coverlet.collector` to the rest of the system?**
  _165 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `SearchProfile Handler` be split into smaller, more focused modules?**
  _Cohesion score 0.08282828282828283 - nodes in this community are weakly interconnected._
- **Why does `IJobSource` connect `Use-Case Handlers` to `Challenge Detection`, `Adapter Blueprint`, `Architecture & Quality Gates`, `Source Contracts`, `Compliance & Risk`?**
  _High betweenness centrality (0.129) - this node is a cross-community bridge._
- **Should `Result Contracts` be split into smaller, more focused modules?**
  _Cohesion score 0.07402031930333818 - nodes in this community are weakly interconnected._
- **Why does `Clean Architecture dependency rule` connect `Architecture & Quality Gates` to `Documentation Index`?**
  _High betweenness centrality (0.106) - this node is a cross-community bridge._