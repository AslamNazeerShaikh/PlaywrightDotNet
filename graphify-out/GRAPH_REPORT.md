# Graph Report - PlaywrightDotNet  (2026-10-29)

## Corpus Check
- 27 files · ~48,962 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 303 nodes · 391 edges · 30 communities (15 shown, 15 thin omitted)
- Extraction: 72% EXTRACTED · 28% INFERRED · 0% AMBIGUOUS · INFERRED: 111 edges (avg confidence: 0.86)
- Token cost: 52,000 input · 24,000 output

## Community Hubs (Navigation)
- Scrape Run Orchestration
- Source Adapter Contracts
- Delivery Gameplan
- Adapter Implementation Blueprint
- Architecture Decisions
- Web Cockpit
- Architecture & Quality Gates
- Compliance Non-Negotiables
- Compliance & Risk
- Documentation Index
- Browser Automation
- Circuit Breaker & Sessions
- GitHub MCP Config
- Clean Architecture
- HTML Parsing Pipeline
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

## God Nodes (most connected - your core abstractions)
1. `Web cockpit (v2 control plane)` - 19 edges
2. `RunScrapeHandler` - 15 edges
3. `JobLens` - 12 edges
4. `Phase 0 open decisions (D1-D12 with recommended defaults)` - 11 edges
5. `IJobSource` - 10 edges
6. `React + Bun web cockpit (v2.0)` - 9 edges
7. `Common adapter blueprint` - 8 edges
8. `Non-negotiables` - 8 edges
9. `ScrapeRun` - 7 edges
10. `JobPosting` - 7 edges

## Surprising Connections (you probably didn't know these)
- `JobLens` --conceptually_related_to--> `Clean Architecture dependency rule`  [INFERRED]
  README.md → docs/02-architecture.md
- `v1 Scope Boundary` --references--> `JobLens`  [EXTRACTED]
  docs/00-executive-summary.md → README.md
- `v1 Success Criteria` --references--> `JobLens`  [EXTRACTED]
  docs/00-executive-summary.md → README.md
- `Clean Architecture layers (Domain, Application, Infrastructure, Cli)` --conceptually_related_to--> `CLI-first (System.CommandLine + Hosting + Serilog)`  [INFERRED]
  docs/10-adr/0004-modular-monolith-clean-architecture.md → docs/10-adr/0008-cli-first-api-later.md
- `Session lifecycle state machine` --semantically_similar_to--> `BrowserProfile`  [INFERRED] [semantically similar]
  docs/07-anti-bot-and-compliance.md → docs/03-domain-model.md

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **ADR index covering ADR-0001 through ADR-0010** — docs_10_adr_readme_architecture_decision_record, docs_10_adr_0001_dotnet_10_lts_dotnet_10_lts, docs_10_adr_0002_xunit_v3_xunit_net_v3, docs_10_adr_0003_playwright_dotnet_playwright_for_dotnet, docs_10_adr_0004_modular_monolith_clean_architecture_modular_monolith, docs_10_adr_0005_sqlite_ef_core_sqlite, docs_10_adr_0006_browser_first_optional_api_fast_path_browser_first, docs_10_adr_0007_compliance_first_low_signal_automation_compliance_first_policy, docs_10_adr_0008_cli_first_api_later_cli_first, docs_10_adr_0009_test_pyramid_coverage_gates_test_pyramid, docs_10_adr_0010_react_bun_cockpit_react_bun_cockpit [EXTRACTED 1.00]
- **Phase 0 recommended default decision set (D1-D12)** — docs_11_open_decisions_joblens_project_name, docs_11_open_decisions_linkedin_opt_in, docs_11_open_decisions_indeed_opt_in, docs_11_open_decisions_full_posting_fields, docs_11_open_decisions_csv_excel_output, docs_11_open_decisions_os_scheduler, docs_11_open_decisions_normal_network_egress, docs_11_open_decisions_bundled_chromium_default, docs_11_open_decisions_recruiter_emails_out_of_scope, docs_11_open_decisions_data_retention_policy [EXTRACTED 1.00]
- **Cockpit control-plane request and live-update flow** — docs_12_web_cockpit_cockpit, docs_12_web_cockpit_dotnet_api_command_surface, docs_10_adr_0010_react_bun_cockpit_bun_bff, docs_12_web_cockpit_sse_event_stream, docs_12_web_cockpit_auth_csrf_model, docs_12_web_cockpit_tanstack_query_state [INFERRED 0.95]
- **Challenge detection to stop-on-wall response flow** — docs_02_architecture_challengedetector, docs_07_anti_bot_and_compliance_challengeassessment, docs_02_architecture_artifactstore, docs_07_anti_bot_and_compliance_circuit_breaker, docs_07_anti_bot_and_compliance_cooldown_policy [EXTRACTED 1.00]
- **Per-source adapter extraction pipeline** — docs_08_site_playbooks_urlbuilder, docs_08_site_playbooks_searchpageparser, docs_08_site_playbooks_normalizer, docs_08_site_playbooks_challengeclassifier, docs_02_architecture_searchpageresult, docs_08_site_playbooks_contract_tests [EXTRACTED 1.00]
- **Compliance gating stack for risky sources** — docs_07_anti_bot_and_compliance_source_risk_tiers, docs_07_anti_bot_and_compliance_compliancepolicy, docs_08_site_playbooks_linkedin_consent_gate, docs_07_anti_bot_and_compliance_fair_use_checklist [EXTRACTED 1.00]

## Communities (30 total, 15 thin omitted)

### Community 0 - "Scrape Run Orchestration"
Cohesion: 0.08
Nodes (32): ArtifactStore, CompliancePolicy, Idempotent upsert by (SourceKey, ExternalJobId), IJobPostingRepository, JobNormalizer, JobPosting aggregate, RunScrapeHandler, JobLens Bounded Contexts (+24 more)

### Community 1 - "Source Adapter Contracts"
Cohesion: 0.09
Nodes (24): Akamai Bot Manager, Cloudflare Bot Management, Naukri Private Search API (jobapi/v3/search), ChallengeAssessment, ChallengeDetector, ChallengeKind, IJobSource, JobSnapshot (+16 more)

### Community 2 - "Delivery Gameplan"
Cohesion: 0.08
Nodes (24): ADR-0010 React + Bun cockpit, v2 Web Cockpit, Decision gates G0–G5, First 15 commits, Phase 0 — Foundation, Phase 1 — Domain + Application, Phase 2 — Browser + Session, Phase 3 — Naukri slice (+16 more)

### Community 3 - "Adapter Implementation Blueprint"
Cohesion: 0.09
Nodes (26): JobSourceContractTests, Challenge kinds taxonomy, ChallengeAssessment (kind, evidence, confidence), ChallengeDetector (anti-bot section 9.3), Acceptance matrix, ChallengeClassifier, Common adapter blueprint, JobSourceContractTests (+18 more)

### Community 4 - "Architecture Decisions"
Cohesion: 0.11
Nodes (18): xUnit.net v3, Playwright for .NET, Optional Naukri API fast path (jobapi/v3/search with RSA nkparam), Per-source compliance tiers and consent gates, Manual/nightly live canaries (never in CI), Local fixture server for Playwright E2E, Risk-based coverage gates (Domain 95/90, Application 90/85, overall 85/80), Bundled Chromium default browser (D9) (+10 more)

### Community 5 - "Web Cockpit"
Cohesion: 0.15
Nodes (20): Deferred API/dashboard (post-Phase 3), Same-origin Bun BFF, React + Bun web cockpit (v2.0), shadcn/ui copy-in components, HTTP/JSON commands + Server-Sent Events transport, Architecture Decision Record (ADR), OS scheduler for scheduling (D6), Cockpit auth: loopback bind, HttpOnly cookie, CSRF double-submit, BFF bearer token (+12 more)

### Community 6 - "Architecture & Quality Gates"
Cohesion: 0.12
Nodes (16): v1 Success Criteria, SQLite + EF Core 10, NetArchTest architecture tests, JobLens.Application, JobLens CLI (composition root), JobLens.Domain, JobLens.Infrastructure, ScrapeRun aggregate (+8 more)

### Community 7 - "Compliance Non-Negotiables"
Cohesion: 0.15
Nodes (8): v1 Scope Boundary, Anti-ban by being a good citizen, Browser hygiene standard, Detection surface matrix, Inert stealth init script finding, Profile isolation per source, Public diagnostic sites (BotD, CreepJS), Old Python project (PythonWebScraping)

### Community 8 - "Compliance & Risk"
Cohesion: 0.16
Nodes (15): CompliancePolicy, ComplianceTier, R1 LinkedIn ToS/legal exposure, R2 Indeed Cloudflare blocks, R4 Selector/DOM drift, Risk register, CompliancePolicy gate, Fair use checklist (+7 more)

### Community 10 - "Browser Automation"
Cohesion: 0.20
Nodes (12): Playwright for .NET, Public Browser Diagnostics, WebKit Is Not Safari, xUnit.net v3, BrowserSessionFactory, ISessionManager, BrowserProfile, Local Fixture Server (+4 more)

### Community 11 - "Circuit Breaker & Sessions"
Cohesion: 0.22
Nodes (10): CircuitBreaker (per source), Exponential backoff policy, Challenge and block metrics, Per-source circuit breaker, Cooldown policy, doctor command, Incident runbook, Review cadence (+2 more)

### Community 12 - "GitHub MCP Config"
Cohesion: 0.20
Nodes (9): headers, oauth, type, url, Authorization, mcp, servers, $schema (+1 more)

### Community 13 - "Clean Architecture"
Cohesion: 0.25
Nodes (8): .NET 10 LTS, global.json SDK pin (10.0.403, rollForward latestFeature), Clean Architecture layers (Domain, Application, Infrastructure, Cli), Focused (tactical) DDD for Sourcing and Access, Modular monolith, EF Core 10.0.14, SQLite as source of truth, WAL single-writer configuration

### Community 14 - "HTML Parsing Pipeline"
Cohesion: 0.40
Nodes (5): HtmlParser, JSON-LD extraction, Visibility guards, Honeypot and visibility guards, SearchPageParser

### Community 17 - "Definition of Done"
Cohesion: 0.67
Nodes (3): Backlog epics E0–E9, Definition of Ready / Done, Coverage and quality metrics

## Knowledge Gaps
- **83 isolated node(s):** `$schema`, `type`, `url`, `oauth`, `Authorization` (+78 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 100 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **15 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Are the 4 inferred relationships involving `RunScrapeHandler` (e.g. with `ChallengePolicy` and `Per-Source Circuit Breaker`) actually correct?**
  _`RunScrapeHandler` has 4 INFERRED edges - model-reasoned connections that need verification._
- **What connects `$schema`, `type`, `url` to the rest of the system?**
  _83 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Scrape Run Orchestration` be split into smaller, more focused modules?**
  _Cohesion score 0.07563025210084033 - nodes in this community are weakly interconnected._
- **Should `Source Adapter Contracts` be split into smaller, more focused modules?**
  _Cohesion score 0.08994708994708994 - nodes in this community are weakly interconnected._
- **Should `Delivery Gameplan` be split into smaller, more focused modules?**
  _Cohesion score 0.082010582010582 - nodes in this community are weakly interconnected._
- **Should `Adapter Implementation Blueprint` be split into smaller, more focused modules?**
  _Cohesion score 0.09230769230769231 - nodes in this community are weakly interconnected._
- **Should `Architecture Decisions` be split into smaller, more focused modules?**
  _Cohesion score 0.11067193675889328 - nodes in this community are weakly interconnected._