# Backend Simplification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce structural complexity in the scrape path plus its two adjacent seams while preserving observable behavior.

**Architecture:** Behavior-preserving internal refactors only: concentrate RunScope ownership, redraw the session seam per source, dissolve the shallow driver, share one permission decision, collapse single-use status records. No mediator, no new packages, no public HTTP/MCP shape change.

**Tech Stack:** .NET 10, xUnit v3 native MTP, EF Core SQLite, Playwright, explicit `HandleAsync` use-case handlers (ADR-0011), block-scoped namespaces + `Program.Main` (ADR-0015).

**Spec:** Architecture review `/var/folders/9c/bv2r2tms5v94sxwfcv_fxqrc0000gn/T/architecture-review-20261108-0835.html` (5 candidates, top recommendation candidate 1), narrator's map `docs/superpowers/specs/2026-11-08-handler-map-design.md` (approved — approach (b) first, refactors guided by it), plus this plan. Executors read all three.

## Global Constraints

- Public HTTP/MCP response shapes stay stable; `IJobSource` port and persistence schema unchanged.
- No MediatR, `IRequest`/`IRequestHandler`, `IMediator`, command/query buses, AutoMapper, or generic repositories (ADR-0011).
- Block-scoped namespaces and explicit `Program.Main`; no top-level or file-scoped namespaces (ADR-0015).
- `dotnet test --solution src/backend/JobScrape.slnx` must stay green after every task.
- One logical change per commit; Conventional Commits format enforced by `.githooks` hook.
- `graphify-out/*` stays dirty and uncommitted (generated hook output).

## Review Focus

- Stale-date vs wall classification on page 2→3 transition still reports `HitWall` rather than throwing — reasonable person expects a recorded outcome, not a crash.
- Naukri warm-up verification still runs after the seam move — reasonable person expects a clean home page check, not a silent skip.
- Offset cursor `start` beyond range still yields offset 0 rather than negative — reasonable person expects first-page fallback.
- Caller with `MustChangePassword=true` still gets `ACCESS_DENIED` even with the right permission string — reasonable person expects denial.
- Source that never ran still reports zero usage and closed circuit without writes — reasonable person expects read-only status.

---

### Task 0: Commit the approved narrator's map (no code)

**Files:**
- Add: `docs/superpowers/specs/2026-11-08-handler-map-design.md`
- Verify against: `src/backend/JobScrape.Api/Controllers/RunsController.cs`, `src/backend/JobScrape.Infrastructure/Runtime/RunCoordinator.cs`, `src/backend/JobScrape.Infrastructure/Composition/UseCaseRegistration.cs`

**Interfaces:**
- Consumes: 32-handler inventory (Scraping 1, Runs 6, Identity 13, Sessions 4, SearchProfiles 4, Sources 1, Quotas 2, Health 1).
- Produces: committed map doc the refactor tasks narrate from.

- [ ] **Step 1: Spot-check three caller rows in the map**

Run: `grep -rn "_start.HandleAsync\|_list.HandleAsync" src/backend/JobScrape.Api/Controllers/RunsController.cs | head -5`
Expected: call sites matching the map's "Called by" cells

- [ ] **Step 2: Run full suite to confirm green baseline before refactors**

Run: `dotnet test --solution src/backend/JobScrape.slnx`
Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add docs/superpowers/specs/2026-11-08-handler-map-design.md docs/superpowers/plans/2026-11-08-backend-simplification.md
git commit -m "docs(specs): add handler narrator's map and simplification plan"
```

---

### Task 1: Concentrate RunScrapeHandler partials into one deep module

**Files:**
- Modify: `src/backend/JobScrape.Application/Features/Scraping/RunScrapeHandler.cs:25-60,196-240,395-430`
- Modify: `src/backend/JobScrape.Application/Features/Scraping/SearchPaginator.cs`
- Modify: `src/backend/JobScrape.Application/Features/Scraping/DetailEnricher.cs`
- Modify: `src/backend/JobScrape.Application/Features/Scraping/RunGate.cs`
- Modify: `src/backend/JobScrape.Application/Features/Scraping/WallAndRetry.cs`
- Test: `src/backend/JobScrape.Application.Tests/Scraping/RunScrapeHandlerTests.cs`

**Interfaces:**
- Consumes: `ScrapeStores`, `JobSourceRegistry`, `SourceSettingsCatalog`, `ISessionManager`, existing `RunScope.Touched: List<JobPosting>` + `RunScope.HitWall: bool` semantics.
- Produces: `RunScrapeHandler.HandleAsync(CancellationToken) -> Result<ScrapeRunSummary>` unchanged; internal `RunScope` transitions owned by one module; `IsStale(...)` and `IsSkillMismatch(...)` remain the only predicates outside.

- [ ] **Step 1: Pin the ordering behavior with a failing characterization test**

```csharp
[Fact]
public async Task Wall_before_page3_records_outcome_without_throwing()
{
    var result = await _handler.HandleAsync(TestContext.Current.CancellationToken);
    Assert.True(result.IsSuccess);
}
```

- [ ] **Step 2: Run to verify baseline**

Run: `dotnet test --solution src/backend/JobScrape.slnx --project JobScrape.Application.Tests`
Expected: PASS (pins current green before refactor)

- [ ] **Step 3: Implement `RunScrapeHandler` as the single RunScope owner in `src/backend/JobScrape.Application/Features/Scraping/RunScrapeHandler.cs`**

Move pagination/enrichment/wall mutation order (`Touched`/`HitWall`/save-after-every-page) into `ExecuteAsync(RunScope, CancellationToken)`; partials become private helpers or fold in; keep `IsStale`/`IsSkillMismatch` predicates outside.

- [ ] **Step 4: Run tests to verify no behavior change**

Run: `dotnet test --solution src/backend/JobScrape.slnx --project JobScrape.Application.Tests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/backend/JobScrape.Application/Features/Scraping/ src/backend/JobScrape.Application.Tests/Scraping/RunScrapeHandlerTests.cs
git commit -m "refactor(scrape): concentrate RunScope ownership in RunScrapeHandler"
```

### Task 2: Redraw the browser session seam per source

**Files:**
- Modify: `src/backend/JobScrape.Infrastructure/Browser/PlaywrightSessionManager.cs:18,78-99`
- Modify: `src/backend/JobScrape.Infrastructure/Runtime/RunCoordinator.cs:21-43`
- Modify: `src/backend/JobScrape.Application/Abstractions/ISessionManager.cs`
- Test: existing Browser/Runtime tests under `src/backend/JobScrape.Infrastructure.Tests/`

**Interfaces:**
- Consumes: `IBrowserSessionPool.Find(BrowserProfileId)`, `NaukriChallengeClassifier.Classify(...)` (moves into adapter).
- Produces: `ISessionManager` keeps its existing method signatures; per-source warm-up lives in its adapter (`NaukriAdapter.VerifyAsync`, `IndeedAdapter.VerifyAsync`, `LinkedInAdapter.VerifyAsync`) behind the existing seam.

- [ ] **Step 1: Write the failing seam test**

```csharp
[Fact]
public async Task Non_naukri_source_verifies_without_naukri_classifier()
{
    var result = await _sessions.VerifyAsync(SourceKey.Indeed, TestContext.Current.CancellationToken);
    Assert.True(result.Verified);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --solution src/backend/JobScrape.slnx --project JobScrape.Infrastructure.Tests --filter "Non_naukri_source_verifies"`
Expected: FAIL (classifier still hardcoded in manager)

- [ ] **Step 3: Implement per-source `VerifyAsync` adapters and thin `PlaywrightSessionManager` to open/verify/release delegation in `src/backend/JobScrape.Infrastructure/Browser/PlaywrightSessionManager.cs`**

Remove `using JobScrape.Infrastructure.Sources.Naukri;` and the `if (source != SourceKey.Naukri)` branch; dispatch to the source adapter's verify routine.

- [ ] **Step 4: Run tests to verify it passes**

Run: `dotnet test --solution src/backend/JobScrape.slnx --project JobScrape.Infrastructure.Tests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/backend/JobScrape.Infrastructure/Browser/ src/backend/JobScrape.Infrastructure/Runtime/ src/backend/JobScrape.Application/Abstractions/ISessionManager.cs
git commit -m "refactor(browser): move warm-up verification into source adapters"
```

### Task 3: Dissolve SearchPageDriver into explicit adapters

**Files:**
- Modify: `src/backend/JobScrape.Infrastructure/Sources/SearchPageDriver.cs:132-147`
- Modify: `src/backend/JobScrape.Infrastructure/Sources/Naukri/NaukriJobSource.cs`
- Modify: `src/backend/JobScrape.Infrastructure/Sources/Indeed/IndeedJobSource.cs`
- Modify: `src/backend/JobScrape.Infrastructure/Sources/LinkedIn/LinkedInJobSource.cs`
- Test: `src/backend/JobScrape.Infrastructure.Tests/Sources/Naukri/NaukriJobSourceTests.cs` (plus Indeed/LinkedIn equivalents)

**Interfaces:**
- Consumes: `SearchPageDriver.FindPage(pool, profileId)`, `WaitForCardsAsync(page, cardSelector)`, `ScrollAsync(page, ct)`, `CursorOffset(cursor)`.
- Produces: each `*JobSource.SearchAsync(...) -> SearchPageResult` owns goto/classify/parse/next-cursor inline; `SearchOffsetPageAsync<TPage>` deleted; small helpers (`FindPage`, `WaitForCardsAsync`, `ScrollAsync`, `CursorOffset`, `Transient`) retained.

- [ ] **Step 1: Write the failing explicit-adapter test**

```csharp
[Fact]
public async Task Indeed_search_owns_its_next_cursor_without_generic_driver()
{
    var page = await _indeed.SearchAsync("dotnet", null, TestContext.Current.CancellationToken);
    Assert.NotNull(page.NextCursor);
}
```

- [ ] **Step 2: Run test to verify current pass-through**

Run: `dotnet test --solution src/backend/JobScrape.slnx --project JobScrape.Infrastructure.Tests --filter "Indeed_search_owns"`
Expected: PASS (pins behavior before dissolve)

- [ ] **Step 3: Implement inline fetch in each `*JobSource.cs`: delete `SearchOffsetPageAsync<TPage>` calls, keep `FindPage`/`WaitForCardsAsync`/`ScrollAsync` helpers**

Each adapter calls goto → wait → scroll → classify → parse → next-cursor directly with its own selector/script/parser.

- [ ] **Step 4: Run tests to verify it passes**

Run: `dotnet test --solution src/backend/JobScrape.slnx --project JobScrape.Infrastructure.Tests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/backend/JobScrape.Infrastructure/Sources/
git commit -m "refactor(sources): dissolve generic SearchOffsetPageAsync into adapters"
```

### Task 4: One permission-decision module for both edges

**Files:**
- Create: `src/backend/JobScrape.Domain/Access/PermissionDecision.cs`
- Modify: `src/backend/JobScrape.Mcp/Tools/McpAuth.cs:22`
- Modify: `src/backend/JobScrape.Api/Http/Auth/PermissionAuthorizationHandler.cs`
- Modify: `src/backend/JobScrape.Application/Features/Identity/LoginHandler.cs` (move dangling-role pruning out)
- Test: `src/backend/JobScrape.Domain.Tests/Access/PermissionDecisionTests.cs` (new) + `src/backend/JobScrape.Api.Tests/AuthEndpointTests.cs`

**Interfaces:**
- Consumes: `PermissionCatalog` keys (e.g. `runs.write`), `ICallerContext.Permissions`, `MustChangePassword`.
- Produces: `PermissionDecision.IsAllowed(ICallerContext caller, string permission) -> bool`; `McpAuth.RequirePermission(caller, permission)` and HTTP handler both delegate to it; no raw-string drift.

- [ ] **Step 1: Write the failing decision test**

```csharp
[Fact]
public void Must_change_password_denies_even_with_permission()
{
    Assert.False(PermissionDecision.IsAllowed(_callerWithMustChange, "runs.write"));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --solution src/backend/JobScrape.slnx --project JobScrape.Domain.Tests --filter "Must_change_password_denies"`
Expected: FAIL with "PermissionDecision not defined"

- [ ] **Step 3: Implement `PermissionDecision.IsAllowed(ICallerContext, string) -> bool` in `src/backend/JobScrape.Domain/Access/PermissionDecision.cs`**

Deny when `MustChangePassword` or permission absent (ordinal compare); rewire `McpAuth.RequirePermission` and HTTP handler to call it; extract login pruning to a pure function.

- [ ] **Step 4: Run tests to verify it passes**

Run: `dotnet test --solution src/backend/JobScrape.slnx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/backend/JobScrape.Domain/Access/PermissionDecision.cs src/backend/JobScrape.Mcp/Tools/McpAuth.cs src/backend/JobScrape.Api/Http/Auth/ src/backend/JobScrape.Application/Features/Identity/LoginHandler.cs
git commit -m "refactor(auth): share one permission decision across HTTP and MCP"
```

### Task 5: Collapse source-status records into one query module

**Files:**
- Modify: `src/backend/JobScrape.Application/Features/Sources/ListSourcesHandler.cs:59-112`
- Modify: `src/backend/JobScrape.Application/Features/Sources/SourceGate.cs`
- Modify: `src/backend/JobScrape.Application/Features/Sources/SourcePolicy.cs`
- Modify: `src/backend/JobScrape.Application/Features/Sources/SourceQuotaUsage.cs`
- Modify: `src/backend/JobScrape.Application/Features/Sources/SourceCircuit.cs`
- Modify: `src/backend/JobScrape.Application/Features/Sources/SourceStatusResponse.cs`
- Test: existing Sources handler tests under `src/backend/JobScrape.Application.Tests/`

**Interfaces:**
- Consumes: `SourceSettingsCatalog.All`, `CompliancePolicy.CanRun`, `ISourceQuotaRepository`, `ISourceCircuitBreakerRepository`, `TimeProvider`.
- Produces: `ListSourcesHandler.HandleAsync(CancellationToken) -> SourceListResponse` unchanged; single-use records fold into the handler module; `SourceSettingsCatalog` stays the retained seam.

- [ ] **Step 1: Write the failing read-only test**

```csharp
[Fact]
public async Task Never_ran_source_reports_zero_usage_closed_circuit_without_writes()
{
    var response = await _handler.HandleAsync(TestContext.Current.CancellationToken);
    Assert.Contains(response.Items, i => i.Quota.PagesFetched == 0);
}
```

- [ ] **Step 2: Run test to verify baseline**

Run: `dotnet test --solution src/backend/JobScrape.slnx --project JobScrape.Application.Tests --filter "Never_ran_source"`
Expected: PASS (pins read-only contract)

- [ ] **Step 3: Implement folded status construction inside `ListSourcesHandler.cs:59-112`; delete single-use record files that add no seam**

Inline `SourceGate`/`SourcePolicy`/`SourceQuotaUsage`/`SourceCircuit` shaping into the handler; keep `SourceStatusResponse` + `SourceListResponse`.

- [ ] **Step 4: Run tests to verify it passes**

Run: `dotnet test --solution src/backend/JobScrape.slnx --project JobScrape.Application.Tests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/backend/JobScrape.Application/Features/Sources/
git commit -m "refactor(sources): collapse single-use status records into handler"
```
