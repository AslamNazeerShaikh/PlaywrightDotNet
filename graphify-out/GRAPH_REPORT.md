# Graph Report - PlaywrightDotNet  (2026-10-31)

## Corpus Check
- 54 files · ~68,057 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 653 nodes · 867 edges · 50 communities (35 shown, 15 thin omitted)
- Extraction: 85% EXTRACTED · 15% INFERRED · 0% AMBIGUOUS · INFERRED: 127 edges (avg confidence: 0.87)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Result Contracts
- Health Slice
- Error Taxonomy
- Runtime Hardening
- Delivery Gameplan
- Adapter Implementation Blueprint
- Web Cockpit
- Architecture Decisions
- Documentation Index
- Application Patterns
- Browser Sessions
- Architecture & Quality Gates
- Compliance Non-Negotiables
- Use-Case Handlers
- JobScrape Solution
- Library Policy
- Conventional Commits
- Compliance Non-Negotiables
- Application Test Packages
- GitHub MCP Config
- Launch Profiles
- Source Playbooks
- Posting Deduplication
- No CQRS ADR
- Serilog Polly ADR
- AOT Policy ADR
- API Test Packages
- Architecture Test Packages
- Libraries As Designed ADR
- Domain Test Packages
- Run Quotas
- Compliance & Risk
- Adapter Contracts
- Search Profiles
- Persistence Unit of Work
- Retry & Page Fetching
- Definition of Done
- Detection Surface
- Browser Profile Credential
- SDK & Package Pinning
- Scope & YAGNI
- CI Canary Policy
- AngleSharp Parsing
- Excel Export
- Pre-Phase Spikes
- CLI Framework
- Time Provider Injection
- ADR Index

## God Nodes (most connected - your core abstractions)
1. `Web cockpit (v2 control plane)` - 18 edges
2. `ErrorKind` - 16 edges
3. `RunScrapeHandler` - 15 edges
4. `Phase 0 open decisions (D1-D12 with recommended defaults)` - 14 edges
5. `JobScrape.Architecture.Tests` - 13 edges
6. `JobLens` - 12 edges
7. `JobScrape.Application.Tests` - 11 edges
8. `JobScrape.Api.Tests` - 10 edges
9. `2. Structured logging with Serilog` - 10 edges
10. `Runtime Hardening: Logging, Errors, Resilience, and Resources` - 10 edges

## Surprising Connections (you probably didn't know these)
- `3.5 Rules` --references--> `DomainException`  [INFERRED]
  docs/14-application-patterns.md → src/backend/JobScrape.Domain/DomainException.cs
- `3.1 Rules` --references--> `DomainException`  [INFERRED]
  docs/15-runtime-hardening.md → src/backend/JobScrape.Domain/DomainException.cs
- `3.3 Error codes` --references--> `ErrorKind`  [INFERRED]
  docs/14-application-patterns.md → src/backend/JobScrape.Application/Results/ErrorKind.cs
- `6.7 Testing` --references--> `ErrorKind`  [INFERRED]
  docs/14-application-patterns.md → src/backend/JobScrape.Application/Results/ErrorKind.cs
- `MTP CodeCoverage extension` --references--> `Microsoft.Testing.Extensions.CodeCoverage`  [EXTRACTED]
  docs/10-adr/0015-jobscrape-naming-mtp-tests.md → src/backend/JobScrape.Application.Tests/JobScrape.Application.Tests.csproj

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **ADR index covering ADR-0001 through ADR-0010** — docs_10_adr_readme_architecture_decision_record, docs_10_adr_0001_dotnet_10_lts_dotnet_10_lts, docs_10_adr_0002_xunit_v3_xunit_net_v3, docs_10_adr_0003_playwright_dotnet_playwright_for_dotnet, docs_10_adr_0004_modular_monolith_clean_architecture_modular_monolith, docs_10_adr_0005_sqlite_ef_core_sqlite, docs_10_adr_0006_browser_first_optional_api_fast_path_browser_first, docs_10_adr_0007_compliance_first_low_signal_automation_compliance_first_policy, docs_10_adr_0008_cli_first_api_later_cli_first, docs_10_adr_0009_test_pyramid_coverage_gates_test_pyramid, docs_10_adr_0010_react_bun_cockpit_react_bun_cockpit [EXTRACTED 1.00]
- **Challenge detection to stop-on-wall response flow** — docs_02_architecture_challengedetector, docs_07_anti_bot_and_compliance_challengeassessment, docs_02_architecture_artifactstore, docs_07_anti_bot_and_compliance_circuit_breaker, docs_07_anti_bot_and_compliance_cooldown_policy [EXTRACTED 1.00]
- **Compliance gating stack for risky sources** — docs_07_anti_bot_and_compliance_source_risk_tiers, docs_07_anti_bot_and_compliance_compliancepolicy, docs_08_site_playbooks_linkedin_consent_gate, docs_07_anti_bot_and_compliance_fair_use_checklist [EXTRACTED 1.00]
- **Phase 0 recommended default decision set (D1-D12)** — docs_11_open_decisions_joblens_project_name, docs_11_open_decisions_linkedin_opt_in, docs_11_open_decisions_indeed_opt_in, docs_11_open_decisions_full_posting_fields, docs_11_open_decisions_csv_excel_output, docs_11_open_decisions_os_scheduler, docs_11_open_decisions_normal_network_egress, docs_11_open_decisions_bundled_chromium_default, docs_11_open_decisions_recruiter_emails_out_of_scope, docs_11_open_decisions_data_retention_policy [EXTRACTED 1.00]
- **Per-source adapter extraction pipeline** — docs_08_site_playbooks_urlbuilder, docs_08_site_playbooks_searchpageparser, docs_08_site_playbooks_normalizer, docs_08_site_playbooks_challengeclassifier, docs_02_architecture_searchpageresult, docs_08_site_playbooks_contract_tests [EXTRACTED 1.00]
- **Cockpit control-plane request and live-update flow** — docs_12_web_cockpit_cockpit, docs_12_web_cockpit_dotnet_api_command_surface, docs_10_adr_0010_react_bun_cockpit_bun_bff, docs_12_web_cockpit_sse_event_stream, docs_12_web_cockpit_auth_csrf_model, docs_12_web_cockpit_tanstack_query_state [INFERRED 0.95]

## Communities (50 total, 15 thin omitted)

### Community 0 - "Result Contracts"
Cohesion: 0.06
Nodes (18): JobScrape.Application.Results, JobScrape.Application.Tests, JobScrape.Domain.Tests, JobScrape.Architecture.Tests, Error, Result, Error, IsFailure (+10 more)

### Community 1 - "Health Slice"
Cohesion: 0.06
Nodes (20): JobScrape.Api.Tests, JobScrape.Application.Features.Health, JobScrape.Api.Controllers, JobScrape.Api, ADR-0015: JobScrape solution naming, backend-first scaffold, native MTP tests, Alternatives considered, Consequences, Context (+12 more)

### Community 2 - "Error Taxonomy"
Cohesion: 0.05
Nodes (38): JobScrape.Domain, 3.1 When to use it, 3.2 Shape, 3.3 Error codes, 3.4 Handler example, 3.5 Rules, 3.6 Testing, 3. Result pattern (+30 more)

### Community 3 - "Runtime Hardening"
Cohesion: 0.05
Nodes (38): 1. Baseline decisions, 2.1 Layering, 2.2 Pipeline, 2.3 Bootstrap, 2.4 Enrichers, 2.5 Correlation and scopes, 2.6 Message templates and performance, 2.7 Redaction (+30 more)

### Community 4 - "Delivery Gameplan"
Cohesion: 0.07
Nodes (30): ADR-0010 React + Bun cockpit, v2 Web Cockpit, Decision gates G0–G5, First 15 commits, Phase 0 — Foundation, Phase 1 — Domain + Application, Phase 2 — Browser + Session, Phase 3 — Naukri slice (+22 more)

### Community 5 - "Adapter Implementation Blueprint"
Cohesion: 0.07
Nodes (33): HtmlParser, JobSourceContractTests, JSON-LD extraction, Visibility guards, Challenge kinds taxonomy, ChallengeAssessment (kind, evidence, confidence), ChallengeDetector (anti-bot section 9.3), Detection surface matrix (+25 more)

### Community 6 - "Web Cockpit"
Cohesion: 0.09
Nodes (30): .NET 10 LTS, global.json SDK pin (10.0.403, rollForward latestFeature), Clean Architecture layers (Domain, Application, Infrastructure, Cli), Focused (tactical) DDD for Sourcing and Access, Modular monolith, EF Core 10.0.14, SQLite as source of truth, WAL single-writer configuration (+22 more)

### Community 7 - "Architecture Decisions"
Cohesion: 0.09
Nodes (22): xUnit.net v3, Playwright for .NET, Optional Naukri API fast path (jobapi/v3/search with RSA nkparam), Per-source compliance tiers and consent gates, Manual/nightly live canaries (never in CI), Local fixture server for Playwright E2E, Risk-based coverage gates (Domain 95/90, Application 90/85, overall 85/80), FakeTimeProvider namespace rename (+14 more)

### Community 8 - "Documentation Index"
Cohesion: 0.26
Nodes (3): .NET 10 LTS, JobLens, JobScrape frontend (control plane) — React 19 + Bun

### Community 9 - "Application Patterns"
Cohesion: 0.08
Nodes (24): 1. Pattern map, 2.1 Decision, 2.2 Banned, 2.3 Replacements, 2.4 Why, 2.5 Allowed read/write separation, 2.6 Enforcement, 2. No CQRS, no MediatR (+16 more)

### Community 10 - "Browser Sessions"
Cohesion: 0.11
Nodes (21): Playwright for .NET, Public Browser Diagnostics, WebKit Is Not Safari, xUnit.net v3, BrowserSessionFactory, CircuitBreaker (per source), ISessionManager, BrowserProfile (+13 more)

### Community 11 - "Architecture & Quality Gates"
Cohesion: 0.11
Nodes (17): v1 Success Criteria, SQLite + EF Core 10, NetArchTest architecture tests, JobLens.Application, JobLens CLI (composition root), JobLens.Domain, JobLens.Infrastructure, ScrapeRun aggregate (+9 more)

### Community 12 - "Compliance Non-Negotiables"
Cohesion: 0.14
Nodes (8): v1 Scope Boundary, Anti-ban by being a good citizen, Browser hygiene standard, HumanPacing, Inert stealth init script finding, Pacing and quotas, Profile isolation per source, Old Python project (PythonWebScraping)

### Community 13 - "Use-Case Handlers"
Cohesion: 0.18
Nodes (14): ArtifactStore, ChallengeAssessment, ChallengeDetector, ChallengeKind, CompliancePolicy, IJobSource, JobSnapshot, JobSourceRegistry (+6 more)

### Community 14 - "JobScrape Solution"
Cohesion: 0.17
Nodes (15): Microsoft.AspNetCore.OpenApi, Microsoft.NET.Sdk.Web, JobScrape.Api, net10.0, JobScrape.Application, net10.0, Microsoft.Extensions.Logging.Abstractions, Microsoft.NET.Sdk (+7 more)

### Community 15 - "Library Policy"
Cohesion: 0.14
Nodes (14): 1. Rules, 2. Library-specific guidance, 3. What we still enforce, 4. Reviewing a new library, 5. Related decisions, 6. References, ClosedXML, EF Core and Playwright (+6 more)

### Community 16 - "Conventional Commits"
Cohesion: 0.15
Nodes (13): 1.1 Header, 1.2 Body, 1.3 Footers, 1.4 Breaking changes, 1. Format, 2.1 Scopes, 2. Types used in this repository, 3.1 Examples (+5 more)

### Community 17 - "Compliance Non-Negotiables"
Cohesion: 0.18
Nodes (10): JobLens CLI Surface, joblens doctor, CLI Exit Codes, Cooldown Ladder, Anti-Bot Metrics and Alerts, Retention Policy, OS Scheduling (cron/launchd/Task Scheduler), Operator Security Checklist (+2 more)

### Community 18 - "Application Test Packages"
Cohesion: 0.20
Nodes (10): MTP CodeCoverage extension, Microsoft.Extensions.TimeProvider.Testing, JobScrape.Application.Tests, net10.0, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio (+2 more)

### Community 19 - "GitHub MCP Config"
Cohesion: 0.20
Nodes (9): headers, oauth, type, url, Authorization, mcp, servers, $schema (+1 more)

### Community 20 - "Launch Profiles"
Cohesion: 0.20
Nodes (9): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, profiles, http (+1 more)

### Community 21 - "Source Playbooks"
Cohesion: 0.22
Nodes (9): Akamai Bot Manager, Cloudflare Bot Management, Naukri Private Search API (jobapi/v3/search), PageFingerprint, URL Canonicalization, Indeed India Playbook, Native Next Pagination (Naukri), Naukri Playbook (+1 more)

### Community 22 - "Posting Deduplication"
Cohesion: 0.31
Nodes (9): Idempotent upsert by (SourceKey, ExternalJobId), IJobPostingRepository, JobNormalizer, JobPosting aggregate, ContentHash, DeduplicationPolicy, JobPosting, JobSkill (+1 more)

### Community 23 - "No CQRS ADR"
Cohesion: 0.22
Nodes (9): ADR-0011: Explicit Use-Case Handlers, No CQRS or MediatR, Alternatives considered, Consequences, Context, Decision, Follow-ups, Negative / trade-offs, Positive (+1 more)

### Community 24 - "Serilog Polly ADR"
Cohesion: 0.22
Nodes (9): ADR-0012: Serilog and Polly Runtime Hardening, Alternatives considered, Consequences, Context, Decision, Follow-ups, Negative / trade-offs, Positive (+1 more)

### Community 25 - "AOT Policy ADR"
Cohesion: 0.22
Nodes (9): ADR-0013: AOT-Friendly Code, No NativeAOT Release, Alternatives considered, Consequences, Context, Decision, Follow-ups, Negative / trade-offs, Positive (+1 more)

### Community 26 - "API Test Packages"
Cohesion: 0.22
Nodes (9): Microsoft.AspNetCore.Mvc.Testing, JobScrape.Api.Tests, net10.0, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3 (+1 more)

### Community 27 - "Architecture Test Packages"
Cohesion: 0.22
Nodes (9): NetArchTest.Rules, JobScrape.Architecture.Tests, net10.0, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3 (+1 more)

### Community 28 - "Libraries As Designed ADR"
Cohesion: 0.25
Nodes (8): ADR-0014: Use Libraries as Designed; AOT Is Not a Constraint, Alternatives considered, Consequences, Context, Decision, Negative / trade-offs, Positive, References

### Community 29 - "Domain Test Packages"
Cohesion: 0.25
Nodes (8): JobScrape.Domain.Tests, net10.0, coverlet.collector, Microsoft.NET.Test.Sdk, Microsoft.Testing.Extensions.CodeCoverage, xunit.runner.visualstudio, xunit.v3, Microsoft.NET.Sdk

### Community 30 - "Run Quotas"
Cohesion: 0.29
Nodes (6): JobLens Bounded Contexts, Per-Source Circuit Breaker, QuotaPolicy, RunEvent, ScrapeRun, SourceQuota

### Community 31 - "Compliance & Risk"
Cohesion: 0.43
Nodes (7): CompliancePolicy, ComplianceTier, CompliancePolicy gate, Fair use checklist, robots.txt findings (2026-10-29), Source risk tiers and gates, LinkedIn consent gate

### Community 32 - "Adapter Contracts"
Cohesion: 0.40
Nodes (4): JobSourceContractTests, Common Adapter Blueprint, Selector Hit Logging, Honeypot and Visibility Guards

### Community 33 - "Search Profiles"
Cohesion: 0.40
Nodes (4): PolitenessPolicy, RunLimits, SearchCriteria, SearchProfile

### Community 36 - "Definition of Done"
Cohesion: 0.67
Nodes (3): Backlog epics E0–E9, Definition of Ready / Done, Coverage and quality metrics

## Knowledge Gaps
- **142 isolated node(s):** `Error`, `IsFailure`, `IsSuccess`, `Error`, `IsFailure` (+137 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 311 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **15 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `JobLens` connect `Documentation Index` to `Architecture & Quality Gates`, `Compliance Non-Negotiables`?**
  _High betweenness centrality (0.243) - this node is a cross-community bridge._
- **Are the 2 inferred relationships involving `ErrorKind` (e.g. with `3.3 Error codes` and `6.7 Testing`) actually correct?**
  _`ErrorKind` has 2 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Error`, `IsFailure`, `IsSuccess` to the rest of the system?**
  _142 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Result Contracts` be split into smaller, more focused modules?**
  _Cohesion score 0.05701754385964912 - nodes in this community are weakly interconnected._
- **Why does `IJobSource` connect `Use-Case Handlers` to `Adapter Contracts`, `Adapter Implementation Blueprint`, `Architecture & Quality Gates`, `Source Playbooks`, `Compliance & Risk`?**
  _High betweenness centrality (0.215) - this node is a cross-community bridge._
- **Are the 4 inferred relationships involving `RunScrapeHandler` (e.g. with `ChallengePolicy` and `Per-Source Circuit Breaker`) actually correct?**
  _`RunScrapeHandler` has 4 INFERRED edges - model-reasoned connections that need verification._
- **Should `Health Slice` be split into smaller, more focused modules?**
  _Cohesion score 0.05893719806763285 - nodes in this community are weakly interconnected._