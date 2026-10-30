# Graph Report - PlaywrightDotNet  (2026-10-31)

## Corpus Check
- 24 files · ~68,254 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 682 nodes · 883 edges · 62 communities (36 shown, 26 thin omitted)
- Extraction: 85% EXTRACTED · 15% INFERRED · 0% AMBIGUOUS · INFERRED: 132 edges (avg confidence: 0.86)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Result Contracts
- Health Slice
- Error Taxonomy
- Runtime Hardening
- Adapter Implementation Blueprint
- Documentation Index
- Web Cockpit
- Architecture & Quality Gates
- Build Pipeline
- Delivery Gameplan
- Application Patterns
- Browser Sessions
- Compliance Non-Negotiables
- Use-Case Handlers
- Library Policy
- Conventional Commits
- Source Playbooks
- Operations Runbook
- Backend Projects
- GitHub MCP Config
- Launch Profiles
- Posting Deduplication
- No CQRS ADR
- Serilog Polly ADR
- AOT Policy ADR
- API Test Packages
- Application Test Packages
- Architecture Test Packages
- Libraries As Designed ADR
- Domain Test Packages
- Run Quotas
- Compliance & Risk
- Search Profiles
- API Project
- Design Documents
- Challenge Handling
- Persistence Unit of Work
- Retry & Page Fetching
- Definition of Done
- Detection Surface
- Browser Profile Credential
- Result ErrorInfo Mapping
- Specification Pattern
- SDK & Package Pinning
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
- ADR Index
- Async Resource Management
- Log Redaction
- Frontend Scaffold

## God Nodes (most connected - your core abstractions)
1. `Web cockpit (v2 control plane)` - 18 edges
2. `RunScrapeHandler` - 16 edges
3. `ErrorKind` - 15 edges
4. `Phase 0 open decisions (D1-D12 with recommended defaults)` - 14 edges
5. `ErrorInfo` - 14 edges
6. `JobLens` - 12 edges
7. `IJobSource` - 11 edges
8. `2. Structured logging with Serilog` - 10 edges
9. `Runtime Hardening: Logging, Errors, Resilience, and Resources` - 10 edges
10. `Application Patterns` - 10 edges

## Surprising Connections (you probably didn't know these)
- `3.3 Error codes` --references--> `ErrorKind`  [INFERRED]
  docs/14-application-patterns.md → src/backend/JobScrape.Application/Results/ErrorKind.cs
- `6.7 Testing` --references--> `ErrorKind`  [INFERRED]
  docs/14-application-patterns.md → src/backend/JobScrape.Application/Results/ErrorKind.cs
- `3.5 Rules` --references--> `DomainException`  [INFERRED]
  docs/14-application-patterns.md → src/backend/JobScrape.Domain/DomainException.cs
- `3.1 Rules` --references--> `DomainException`  [INFERRED]
  docs/15-runtime-hardening.md → src/backend/JobScrape.Domain/DomainException.cs
- `v1 Success Criteria` --references--> `JobLens`  [EXTRACTED]
  docs/00-executive-summary.md → README.md

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **ADR index covering ADR-0001 through ADR-0010** — docs_10_adr_readme_architecture_decision_record, docs_10_adr_0001_dotnet_10_lts_dotnet_10_lts, docs_10_adr_0002_xunit_v3_xunit_net_v3, docs_10_adr_0003_playwright_dotnet_playwright_for_dotnet, docs_10_adr_0004_modular_monolith_clean_architecture_modular_monolith, docs_10_adr_0005_sqlite_ef_core_sqlite, docs_10_adr_0006_browser_first_optional_api_fast_path_browser_first, docs_10_adr_0007_compliance_first_low_signal_automation_compliance_first_policy, docs_10_adr_0008_cli_first_api_later_cli_first, docs_10_adr_0009_test_pyramid_coverage_gates_test_pyramid, docs_10_adr_0010_react_bun_cockpit_react_bun_cockpit [EXTRACTED 1.00]
- **Challenge detection to stop-on-wall response flow** — docs_02_architecture_challengedetector, docs_07_anti_bot_and_compliance_challengeassessment, docs_02_architecture_artifactstore, docs_07_anti_bot_and_compliance_circuit_breaker, docs_07_anti_bot_and_compliance_cooldown_policy [EXTRACTED 1.00]
- **Compliance gating stack for risky sources** — docs_07_anti_bot_and_compliance_source_risk_tiers, docs_07_anti_bot_and_compliance_compliancepolicy, docs_08_site_playbooks_linkedin_consent_gate, docs_07_anti_bot_and_compliance_fair_use_checklist [EXTRACTED 1.00]
- **Phase 0 recommended default decision set (D1-D12)** — docs_11_open_decisions_joblens_project_name, docs_11_open_decisions_linkedin_opt_in, docs_11_open_decisions_indeed_opt_in, docs_11_open_decisions_full_posting_fields, docs_11_open_decisions_csv_excel_output, docs_11_open_decisions_os_scheduler, docs_11_open_decisions_normal_network_egress, docs_11_open_decisions_bundled_chromium_default, docs_11_open_decisions_recruiter_emails_out_of_scope, docs_11_open_decisions_data_retention_policy [EXTRACTED 1.00]
- **Per-source adapter extraction pipeline** — docs_08_site_playbooks_urlbuilder, docs_08_site_playbooks_searchpageparser, docs_08_site_playbooks_normalizer, docs_08_site_playbooks_challengeclassifier, docs_02_architecture_searchpageresult, docs_08_site_playbooks_contract_tests [EXTRACTED 1.00]
- **Cockpit control-plane request and live-update flow** — docs_12_web_cockpit_cockpit, docs_12_web_cockpit_dotnet_api_command_surface, docs_10_adr_0010_react_bun_cockpit_bun_bff, docs_12_web_cockpit_sse_event_stream, docs_12_web_cockpit_auth_csrf_model, docs_12_web_cockpit_tanstack_query_state [INFERRED 0.95]
- **Scrape run orchestration flow** — docs_02_architecture_runscrapehandler, docs_02_architecture_ijobsource, docs_02_architecture_challenge_detector [EXTRACTED 1.00]
- **Result to edge error mapping flow** — docs_14_application_patterns_result_pattern, docs_14_application_patterns_problem_details, docs_02_architecture_result_and_problem_details, docs_15_runtime_hardening_global_exception_handling [EXTRACTED 1.00]

## Communities (62 total, 26 thin omitted)

### Community 0 - "Result Contracts"
Cohesion: 0.06
Nodes (20): JobScrape.Application.Results, JobScrape.Application.Tests, JobScrape.Domain.Tests, JobScrape.Architecture.Tests, ErrorInfo, Result, Error, IsFailure (+12 more)

### Community 1 - "Health Slice"
Cohesion: 0.05
Nodes (19): JobScrape.Api.Tests, JobScrape.Application.Features.Health, JobScrape.Api.Controllers, JobScrape.Api, ADR-0015: JobScrape solution naming, backend-first scaffold, native MTP tests, Alternatives considered, Consequences, Context (+11 more)

### Community 2 - "Error Taxonomy"
Cohesion: 0.05
Nodes (37): JobScrape.Domain, 3.1 When to use it, 3.2 Shape, 3.3 Error codes, 3.4 Handler example, 3.5 Rules, 3.6 Testing, 3. Result pattern (+29 more)

### Community 3 - "Runtime Hardening"
Cohesion: 0.05
Nodes (38): 1. Baseline decisions, 2.1 Layering, 2.2 Pipeline, 2.3 Bootstrap, 2.4 Enrichers, 2.5 Correlation and scopes, 2.6 Message templates and performance, 2.7 Redaction (+30 more)

### Community 4 - "Adapter Implementation Blueprint"
Cohesion: 0.07
Nodes (35): Naukri Private Search API (jobapi/v3/search), HtmlParser, JobSourceContractTests, JSON-LD extraction, Visibility guards, Challenge kinds taxonomy, ChallengeAssessment (kind, evidence, confidence), ChallengeDetector (anti-bot section 9.3) (+27 more)

### Community 5 - "Documentation Index"
Cohesion: 0.17
Nodes (12): .NET 10 LTS, Per-source compliance tiers and consent gates, CSV + Excel export (JSON optional, Postgres later), Data retention 30/90/180 days (D12), Full posting field set (D4), Indeed opt-in with conservative caps (D3), JobLens working project name (D1), LinkedIn opt-in high-risk, default disabled (D2) (+4 more)

### Community 6 - "Web Cockpit"
Cohesion: 0.09
Nodes (30): .NET 10 LTS, global.json SDK pin (10.0.403, rollForward latestFeature), Clean Architecture layers (Domain, Application, Infrastructure, Cli), Focused (tactical) DDD for Sourcing and Access, Modular monolith, EF Core 10.0.14, SQLite as source of truth, WAL single-writer configuration (+22 more)

### Community 7 - "Architecture & Quality Gates"
Cohesion: 0.08
Nodes (21): NetArchTest architecture tests, JobLens.Application, JobLens CLI (composition root), JobLens.Domain, JobLens.Infrastructure, ScrapeRun aggregate, SearchProfile aggregate, Solution Layout (JobLens.slnx) (+13 more)

### Community 8 - "Build Pipeline"
Cohesion: 0.08
Nodes (25): v1 Success Criteria, SQLite + EF Core 10, ADR-0010 React + Bun cockpit, ChallengeDetector and ChallengeAssessment, v2 Web Cockpit, CI Pipeline, Stop-on-wall compliance policy, v2 track — Web cockpit (+17 more)

### Community 9 - "Delivery Gameplan"
Cohesion: 0.09
Nodes (21): Modular Monolith with Clean Architecture and inward dependency rule, Decision gates G0–G5, First 15 commits, Phase 0 — Foundation, Phase 1 — Domain + Application, Phase 2 — Browser + Session, Phase 3 — Naukri slice, Phase 4 — Indeed + LinkedIn (+13 more)

### Community 10 - "Application Patterns"
Cohesion: 0.08
Nodes (24): 1. Pattern map, 2.1 Decision, 2.2 Banned, 2.3 Replacements, 2.4 Why, 2.5 Allowed read/write separation, 2.6 Enforcement, 2. No CQRS, no MediatR (+16 more)

### Community 11 - "Browser Sessions"
Cohesion: 0.11
Nodes (21): Playwright for .NET, Public Browser Diagnostics, WebKit Is Not Safari, xUnit.net v3, BrowserSessionFactory, CircuitBreaker (per source), ISessionManager, BrowserProfile (+13 more)

### Community 12 - "Compliance Non-Negotiables"
Cohesion: 0.14
Nodes (8): v1 Scope Boundary, Anti-ban by being a good citizen, Browser hygiene standard, HumanPacing, Inert stealth init script finding, Pacing and quotas, Profile isolation per source, Old Python project (PythonWebScraping)

### Community 13 - "Use-Case Handlers"
Cohesion: 0.20
Nodes (13): ArtifactStore, ChallengeAssessment, ChallengeDetector, ChallengeKind, CompliancePolicy, IJobSource, JobSnapshot, JobSourceRegistry (+5 more)

### Community 14 - "Library Policy"
Cohesion: 0.14
Nodes (14): 1. Rules, 2. Library-specific guidance, 3. What we still enforce, 4. Reviewing a new library, 5. Related decisions, 6. References, ClosedXML, EF Core and Playwright (+6 more)

### Community 15 - "Conventional Commits"
Cohesion: 0.15
Nodes (13): 1.1 Header, 1.2 Body, 1.3 Footers, 1.4 Breaking changes, 1. Format, 2.1 Scopes, 2. Types used in this repository, 3.1 Examples (+5 more)

### Community 16 - "Source Playbooks"
Cohesion: 0.17
Nodes (11): Akamai Bot Manager, Cloudflare Bot Management, PageFingerprint, JobSourceContractTests, Common Adapter Blueprint, URL Canonicalization, Indeed India Playbook, Native Next Pagination (Naukri) (+3 more)

### Community 17 - "Operations Runbook"
Cohesion: 0.18
Nodes (10): JobLens CLI Surface, joblens doctor, CLI Exit Codes, Cooldown Ladder, Anti-Bot Metrics and Alerts, Retention Policy, OS Scheduling (cron/launchd/Task Scheduler), Operator Security Checklist (+2 more)

### Community 18 - "Backend Projects"
Cohesion: 0.20
Nodes (8): net10.0, Microsoft.Extensions.Logging.Abstractions, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.Extensions.Logging.Abstractions, Microsoft.NET.Sdk

### Community 19 - "GitHub MCP Config"
Cohesion: 0.20
Nodes (9): headers, oauth, type, url, Authorization, mcp, servers, $schema (+1 more)

### Community 20 - "Launch Profiles"
Cohesion: 0.20
Nodes (9): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, profiles, http (+1 more)

### Community 21 - "Posting Deduplication"
Cohesion: 0.31
Nodes (9): Idempotent upsert by (SourceKey, ExternalJobId), IJobPostingRepository, JobNormalizer, JobPosting aggregate, ContentHash, DeduplicationPolicy, JobPosting, JobSkill (+1 more)

### Community 22 - "No CQRS ADR"
Cohesion: 0.22
Nodes (9): ADR-0011: Explicit Use-Case Handlers, No CQRS or MediatR, Alternatives considered, Consequences, Context, Decision, Follow-ups, Negative / trade-offs, Positive (+1 more)

### Community 23 - "Serilog Polly ADR"
Cohesion: 0.22
Nodes (9): ADR-0012: Serilog and Polly Runtime Hardening, Alternatives considered, Consequences, Context, Decision, Follow-ups, Negative / trade-offs, Positive (+1 more)

### Community 24 - "AOT Policy ADR"
Cohesion: 0.22
Nodes (9): ADR-0013: AOT-Friendly Code, No NativeAOT Release, Alternatives considered, Consequences, Context, Decision, Follow-ups, Negative / trade-offs, Positive (+1 more)

### Community 25 - "API Test Packages"
Cohesion: 0.22
Nodes (8): Microsoft.AspNetCore.Mvc.Testing, net10.0, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3, Microsoft.NET.Sdk

### Community 26 - "Application Test Packages"
Cohesion: 0.22
Nodes (8): Microsoft.Extensions.TimeProvider.Testing, net10.0, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3, Microsoft.NET.Sdk

### Community 27 - "Architecture Test Packages"
Cohesion: 0.22
Nodes (8): NetArchTest.Rules, net10.0, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3, Microsoft.NET.Sdk

### Community 28 - "Libraries As Designed ADR"
Cohesion: 0.25
Nodes (8): ADR-0014: Use Libraries as Designed; AOT Is Not a Constraint, Alternatives considered, Consequences, Context, Decision, Negative / trade-offs, Positive, References

### Community 29 - "Domain Test Packages"
Cohesion: 0.25
Nodes (7): net10.0, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3, Microsoft.NET.Sdk

### Community 30 - "Run Quotas"
Cohesion: 0.29
Nodes (6): JobLens Bounded Contexts, Per-Source Circuit Breaker, QuotaPolicy, RunEvent, ScrapeRun, SourceQuota

### Community 31 - "Compliance & Risk"
Cohesion: 0.43
Nodes (7): CompliancePolicy, ComplianceTier, CompliancePolicy gate, Fair use checklist, robots.txt findings (2026-10-29), Source risk tiers and gates, LinkedIn consent gate

### Community 32 - "Search Profiles"
Cohesion: 0.40
Nodes (4): PolitenessPolicy, RunLimits, SearchCriteria, SearchProfile

### Community 33 - "API Project"
Cohesion: 0.50
Nodes (3): Microsoft.AspNetCore.OpenApi, Microsoft.NET.Sdk.Web, net10.0

### Community 34 - "Design Documents"
Cohesion: 1.00
Nodes (3): Architecture Document, Application Patterns Document, Runtime Hardening Document

### Community 38 - "Definition of Done"
Cohesion: 0.67
Nodes (3): Backlog epics E0–E9, Definition of Ready / Done, Coverage and quality metrics

## Knowledge Gaps
- **160 isolated node(s):** `Challenge`, `Conflict`, `CoolingDown`, `Forbidden`, `NotFound` (+155 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 338 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **26 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `JobLens` connect `Documentation Index` to `Build Pipeline`, `Compliance Non-Negotiables`, `Architecture & Quality Gates`?**
  _High betweenness centrality (0.231) - this node is a cross-community bridge._
- **Are the 4 inferred relationships involving `RunScrapeHandler` (e.g. with `ChallengePolicy` and `Per-Source Circuit Breaker`) actually correct?**
  _`RunScrapeHandler` has 4 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Challenge`, `Conflict`, `CoolingDown` to the rest of the system?**
  _160 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Result Contracts` be split into smaller, more focused modules?**
  _Cohesion score 0.055944055944055944 - nodes in this community are weakly interconnected._
- **Why does `IJobSource` connect `Use-Case Handlers` to `Source Playbooks`, `Adapter Implementation Blueprint`, `Compliance & Risk`, `Architecture & Quality Gates`?**
  _High betweenness centrality (0.209) - this node is a cross-community bridge._
- **Are the 2 inferred relationships involving `ErrorKind` (e.g. with `3.3 Error codes` and `6.7 Testing`) actually correct?**
  _`ErrorKind` has 2 INFERRED edges - model-reasoned connections that need verification._
- **Should `Health Slice` be split into smaller, more focused modules?**
  _Cohesion score 0.05087881591119334 - nodes in this community are weakly interconnected._