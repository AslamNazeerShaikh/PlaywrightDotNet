# New-only scrape runs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A per-run "new only" choice from the runs start form to the database and back, plus its website diagrams, info tooltips, and visited-link color.

**Architecture:** One boolean rides the existing start path (`StartRunRequest` → `RunScrapeRequest` → `RunScope`), is snapshotted on `ScrapeRun`, exempts known rows from the job cap, and filters the run postings query. Frontend adds the checkbox, a badge, and two Topology diagrams with the shared SVG vocabulary.

**Tech Stack:** .NET 10 (xUnit v3, EF Core SQLite), React 19 + Bun (bun test, Playwright), Serilog, RFC 9457 problems.

**Spec:** `docs/superpowers/specs/2026-11-09-new-only-runs-design.md`

## Global Constraints

- Explicit constructor-injected handlers; `Result<T>` for expected failures; no mediator, no generic repositories.
- API errors as RFC 9457 problems with stable codes; UI renders codes, never prose.
- `CancellationToken` on every I/O boundary; `using`/`await using` for streams.
- No new dependencies for a few lines of code; reuse the codebase helper first.
- `dotnet build` with 0 warnings; `bun check` clean; `bun fmt` applied.
- Every behavior change is TDD: RED test first, one watched fail, then the minimal pass.
- Commits follow `AGENTS.md` Conventional Commits (`<type>(<scope>)!: <description>`, 72-char target); the `commit` step runs the hook and fixes the message if it rejects.
- All backend `dotnet` commands run from `src/backend` (owner of `global.json`, which selects native MTP mode; repo-root invocation rejects `--project`). `--project` values are short project names (e.g. `JobScrape.Application.Tests`); `dotnet ef` `--project`/`--startup-project` likewise.
- Before any commit: `/deslop` reread; no commented-out code; no secrets; full suites green.

## Review Focus

1. `onlyNew` absent from the POST body must mean `false`, not 400. A hand-rolled JSON client omits the field. Pinned in Task 5 (`AcceptsMissingOnlyNewAsFalse`).
2. Rows recorded before the migration read as normal runs, never as new-only. The column default is `false`. Pinned in Task 4 (assert a run inserted without the flag reads `false`).
3. A new-only run over pages of only known rows still completes and still refreshes them. `Complete` needs a fetched page, and `found` counts parsed rows. Pinned in Task 3 (`CompletesWithZeroNewAndRefreshesKnownRows`).
4. A second start while a run executes stays `409 RUN_IN_PROGRESS`, flagged or not. The listing window is time-based, so overlap would leak rows. Pinned in Task 5 (flagged start during an active run → `409`).
5. `FirstSeenAt` equal to the run start, same tick, counts as new (`>=`, inclusive both bounds). Clock granularity makes exact-tick rows real. Pinned in Task 4 (boundary posting appears in the flagged listing).

---

## File structure

- Domain flag: `src/backend/JobScrape.Domain/Sourcing/ScrapeRun.cs` (+ `src/backend/JobScrape.Domain.Tests/Sourcing/ScrapeRunTests.cs`).
- Request path: `Features/Scraping/RunScrapeRequest.cs`, `Features/Runs/StartRunRequest.cs`, `Features/Runs/StartRunHandler.cs` (+ `RunHandlersTests.cs`).
- Scrape behavior: `Features/Scraping/RunScrapeHandler.cs`, `Features/Scraping/ScrapeRunSummary.cs` (+ new `RunScrapeOnlyNewTests.cs`, harness tweak).
- Persistence: `Persistence/Configurations/ScrapeRunConfiguration.cs`, new migration, `Persistence/RunReadStore.cs`, `Features/Runs/RunResponse.cs` (+ new `RunOnlyNewOnSqliteTests.cs`).
- Edge: `Api/Controllers/RunsController.cs` (comment), `Mcp/Tools/RunActionsTools.cs` (+ `RunsEndpointTests.cs`, `RunActionsToolsTests.cs`).
- Deck: `client/lib/api.ts`, `client/pages/runs.tsx`, new `client/components/info-tip.tsx`, `client/styles/globals.css`, `client/pages/topology.tsx` (+ `tests/client/runs.test.tsx`, new `tests/client/topology.test.tsx`, `src/frontend/src/tests/e2e/runs.e2e.ts`).
- Docs: `docs/03-domain-model.md`, `docs/12-web-cockpit.md`, `src/frontend/README.md`, `docs/09-operations-runbook.md` (only files whose prose the builder's grep shows naming the old behavior).
- Profile: `~/.diagram-design/profiles/ops-deck.md` (new), `<repo>/.diagram-design` (new marker).

---

### Task 1: Domain flag on the run

**Files:**
- Modify: `src/backend/JobScrape.Domain/Sourcing/ScrapeRun.cs`
- Test: `src/backend/JobScrape.Domain.Tests/Sourcing/ScrapeRunTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `ScrapeRun.Create(SearchProfile profile, DateTimeOffset now, bool onlyNew = false)`; `bool ScrapeRun.OnlyNew { get; }` — used by Tasks 3, 4, 5.

- [ ] **Step 1: Write the failing tests.** Append to `ScrapeRunTests.cs`:
```csharp
[Fact]
public void CreateDefaultsOnlyNewToFalse()
{
    var run = ScrapeRun.Create(NewProfile(), Now);
    Assert.False(run.OnlyNew);
}

[Fact]
public void CreatePreservesOnlyNew()
{
    var run = ScrapeRun.Create(NewProfile(), Now, onlyNew: true);
    Assert.True(run.OnlyNew);
}
```
Reuse the file's existing `NewProfile()`/`Now` helpers; if names differ, use the file's own profile factory and timestamp. No other assertions.
- [ ] **Step 2: Run to verify they fail.** Run: `dotnet test src/backend/JobScrape.Domain.Tests --filter "FullyQualifiedName~ScrapeRunTests.CreateDefaultsOnlyNewToFalse|FullyQualifiedName~ScrapeRunTests.CreatePreservesOnlyNew"`. Expected: compile FAIL (`OnlyNew` undefined).
- [ ] **Step 3: Implement.** Private ctor gains `bool onlyNew` (assign to get-only `OnlyNew`); `Create` gains `bool onlyNew = false` and forwards it. XML docs on the parameter, the property, and the class snapshot list. Run `dotnet csharpier format` on touched files. Then the persistence slice: `ScrapeRunConfiguration` maps `builder.Property(run => run.OnlyNew);`, scaffold `AddOnlyNewToScrapeRuns` (`dotnet ef migrations add AddOnlyNewToScrapeRuns --project src/backend/JobScrape.Infrastructure --startup-project src/backend/JobScrape.Api` from repo root) and keep the model snapshot in sync. Rationale: EF binds the new ctor parameter only when mapped, and the column only exists with the migration — the three land in one commit so every commit builds the model. Nothing else changes.
- [ ] **Step 4: Run to verify they pass.** Run: `dotnet test --project src/backend/JobScrape.Domain.Tests --filter-class "*ScrapeRunTests*"` (whole class green); `dotnet csharpier check` on touched files; `dotnet ef migrations has-pending-model-changes --project src/backend/JobScrape.Infrastructure --startup-project src/backend/JobScrape.Api` (expect no changes); `dotnet test --project src/backend/JobScrape.Infrastructure.Tests --filter-class "*RunScrapeOnSqliteTests*"` (existing sqlite suite proves the model builds and migrates). Expected: all PASS.
- [ ] **Step 5: Commit.** `git add` the two files. Message per Global Constraints, e.g. `feat(runs)!: snapshot OnlyNew on ScrapeRun`.

### Task 2: Request plumbing

**Files:**
- Modify: `src/backend/JobScrape.Application/Features/Scraping/RunScrapeRequest.cs`, `src/backend/JobScrape.Application/Features/Runs/StartRunRequest.cs`, `src/backend/JobScrape.Application/Features/Runs/StartRunHandler.cs`
- Test: `src/backend/JobScrape.Application.Tests/RunHandlersTests.cs`

**Interfaces:**
- Consumes: Task 1 (nothing yet; records only).
- Produces: `RunScrapeRequest(..., bool OnlyNew = false)`, `StartRunRequest(Guid, Guid, bool Enrich = true, bool OnlyNew = false)`; `StartRunHandler` forwards `request.OnlyNew` — used by Tasks 3, 5.

- [ ] **Step 1: Write the failing tests.** Append facts:
```csharp
[Fact]
public async Task StartForwardsOnlyNewToTheLauncher()
{
    var launcher = new FakeLauncher();
    var handler = new StartRunHandler(launcher, new FakeRunStore(), Consent, Caller);
    var request = new StartRunRequest(Guid.NewGuid(), Guid.NewGuid(), Enrich: false, OnlyNew: true);
    await handler.HandleAsync(request, Ct);
    Assert.True(Assert.Single(launcher.Started).OnlyNew);
}
```
Plus a mirror fact with `OnlyNew: false`. Reuse the file's existing handler-construction helpers and `FakeRunStore`; only the `OnlyNew` assertions are new.
- [ ] **Step 2: Run to verify they fail.** Run from the repo root: `dotnet test --project src/backend/JobScrape.Application.Tests --filter-method "*StartForwardsOnlyNew*"`. Expected: compile FAIL.
- [ ] **Step 3: Implement.** Add `bool OnlyNew = false` to both records with `<param>` docs; `StartRunHandler` passes `request.OnlyNew` into `new RunScrapeRequest(...)`. `RunsController` untouched (same record type).
- [ ] **Step 4: Run to verify they pass.** Run: `dotnet test --project src/backend/JobScrape.Application.Tests --filter-class "*RunHandlersTests*"`. Expected: PASS, including all pre-existing facts.
- [ ] **Step 5: Commit.** `feat(runs)!: carry OnlyNew from start request to scrape request`.

### Task 3: New-only scrape behavior

**Files:**
- Modify: `src/backend/JobScrape.Application/Features/Scraping/RunScrapeHandler.cs`, `src/backend/JobScrape.Application/Features/Scraping/ScrapeRunSummary.cs`, `src/backend/JobScrape.Application.Tests/Scraping/ScrapeHarness.cs` (add `public bool OnlyNew { get; set; }`, include via named argument `OnlyNew: OnlyNew` in `Request` — positional 4th would bind `Enrich`)
- Test: Create `src/backend/JobScrape.Application.Tests/Scraping/RunScrapeOnlyNewTests.cs`

**Interfaces:**
- Consumes: Tasks 1–2 (`ScrapeRun.Create(..., onlyNew)`, `request.OnlyNew`).
- Produces: `RunScope.OnlyNew`; cap-from-`PostingsNew` loop; fresh-first enrichment; `ScrapeRunSummary.OnlyNew` — used by Tasks 4, 5.

- [ ] **Step 1: Write the failing tests.** In the new file, using harness `Job`/`Page` helpers:
  - `KnownRowsRefreshWithoutDuplicating`: seed store with posting "1" (FirstSeenAt past), page has `Job("1")` + `Job("2")`, `OnlyNew = true` → `PostingsNew == 1`, `PostingsFound == 2`, store has 2 rows, row "1" `LastSeenAt` advanced, `FirstSeenAt` unchanged.
  - `KnownRowsDoNotConsumeTheJobCap`: `maxJobs: 2` harness, page 1 has `Job("1")` known + `Job("2")` fresh, page 2 has `Job("3")`, `Job("4")` fresh, `OnlyNew = true` → run `Completed`, `PostingsNew == 2`, both pages fetched.
  - `CompletesWithZeroNewAndRefreshesKnownRows`: page of only known rows → `Completed`, `PostingsNew == 0`, `LastSeenAt` refreshed (Review Focus 3).
  - `EnrichesFreshBeforeKnown`: `maxDetails: 1`, touched enriched... use `Detail()` wiring from `RunScrapeEnrichmentTests` pattern: new-only run where a known row and a fresh row both lack descriptions → the fresh row gains a description, the known row does not.
  - `SummaryCarriesOnlyNew`: `OnlyNew = true` run → `summary.OnlyNew` true; default harness run → false.
- [ ] **Step 2: Run to verify they fail.** Run from `src/backend`: `dotnet test --project JobScrape.Application.Tests --filter-class "*RunScrapeOnlyNewTests*"`. Expected: compile FAIL (`OnlyNew` missing on harness/handler).
- [ ] **Step 3: Implement.**
  - `RunScope` gains `bool OnlyNew` ctor arg from `request.OnlyNew`; `OpenScopeAsync` passes `request.OnlyNew` and calls `ScrapeRun.Create(profile, now, request.OnlyNew)`.
  - `SearchAsync`: `var collected = scope.OnlyNew ? run.Stats.PostingsNew : run.Stats.PostingsFound;` used for the loop-stop check and `remaining`. XML comment: in new-only runs the cap binds new postings, so `PostingsFound` may exceed `MaxJobs`.
  - `UpsertAsync`: thread `scope.OnlyNew`; the in-page `found >= remaining` break becomes `added >= remaining` in new-only mode. Refresh path, `seenJobs`, `Touched`, and `PageStats(found, added, updated)` semantics unchanged.
  - `PendingEnrichment`: in new-only mode order `Touched` fresh-first (added this run first by identity), discovery order within each group.
  - `ScrapeRunSummary` gains `bool OnlyNew` + `From` mapping.
- [ ] **Step 4: Run to verify they pass.** Run from `src/backend`: `dotnet test --project JobScrape.Application.Tests`. Expected: PASS, full project including pre-existing handler, enrichment, and run-handler tests.
- [ ] **Step 5: Commit.** `feat(runs)!: scrape new-only runs without capping on known rows`.

### Task 4: Persistence and read edge

**Files:**
- Modify: `src/backend/JobScrape.Infrastructure/Persistence/Configurations/ScrapeRunConfiguration.cs`, `src/backend/JobScrape.Infrastructure/Persistence/RunReadStore.cs`, `src/backend/JobScrape.Application/Features/Runs/RunResponse.cs`
- Create: migration `AddOnlyNewToScrapeRuns` + model snapshot update
- Test: Create `src/backend/JobScrape.Infrastructure.Tests/Persistence/RunOnlyNewOnSqliteTests.cs`

**Interfaces:**
- Consumes: Tasks 1–3 (flag on run, `FirstSeenAt` semantics).
- Produces: `scrape_runs.only_new` column (default false); `RunResponse.OnlyNew`; filtered `PostingsAsync` — used by Task 5.

- [ ] **Step 1: Write the failing tests.** In the new file (follow `RunScrapeOnSqliteTests` + `TestDatabase` setup):
  - `FlagPersistsAndDefaultsFalse`: save a run without touching the flag → reload → `OnlyNew` false (Review Focus 2); save one with `true` → reload → true.
  - `FlaggedListingHoldsOnlyFirstSeenInWindow`: two runs over one source; run 2 flagged; a posting first seen in run 1 and refreshed in run 2 is absent from run 2's `ListPostingsAsync`; a posting first seen in run 2 is present. Total counts match.
  - `UnflaggedListingIsUnchanged`: same data, unflagged run 2 → both postings listed.
  - `BoundaryTickCountsAsNew`: posting with `FirstSeenAt` equal to the run start appears in the flagged listing (Review Focus 5).
  - `RunResponseCarriesOnlyNew`: `RunResponse.From(flaggedRun, ...)` → `OnlyNew` true.
- [ ] **Step 2: Run to verify they fail.** Run from `src/backend`: `dotnet test --project JobScrape.Infrastructure.Tests --filter-class "*RunOnlyNewOnSqliteTests*"`. Expected: compile FAIL.
- [ ] **Step 3: Implement.** (Mapping and migration already landed in Task 1; do not recreate them.)
  - `RunReadStore.PostingsAsync`: add `&& posting.FirstSeenAt >= from` when `run.OnlyNew` is true, both in the items query and the count query.
  - `RunResponse`: add `bool OnlyNew` + `From` mapping with `<param>` doc.
- [ ] **Step 4: Run to verify they pass.** Run from `src/backend`: `dotnet test --project JobScrape.Infrastructure.Tests`. Expected: PASS, including `TwoRunsPersistDedupeAndChargeTheDailyQuota` unchanged.
- [ ] **Step 5: Commit.** `feat(runs)!: persist OnlyNew and filter flagged run listings`.

### Task 5: API and MCP surface

**Files:**
- Modify: `src/backend/JobScrape.Api/Controllers/RunsController.cs` (start doc comment names `onlyNew`), `src/backend/JobScrape.Mcp/Tools/RunActionsTools.cs` (`bool onlyNew = false` + pass-through + doc)
- Test: `src/backend/JobScrape.Api.Tests/RunsEndpointTests.cs`, `src/backend/JobScrape.Mcp.Tests/RunActionsToolsTests.cs`

**Interfaces:**
- Consumes: Tasks 1–4.
- Produces: `POST /api/v1/runs` accepts `onlyNew`; `start_run(onlyNew)` — used by Task 6.

- [ ] **Step 1: Write the failing tests.**
  - API `SecondRunWithOnlyNewListsOnlyFreshThroughSqlite`: seed via `SeedAsync`; start run 1 (no flag) with `FakeSource`, wait terminal; start run 2 with `{ profileId, browserProfileId, onlyNew: true }`, wait terminal → `done.OnlyNew` true, `PostingsNew` counts only fresh, `GET postings` holds only fresh ids with `IsNew` true, `Total` matches, CSV export contains a fresh URL and not a known-only URL.
  - API `AcceptsMissingOnlyNewAsFalse`: POST without the field → `202`, run `OnlyNew` false (Review Focus 1).
  - API `FlaggedStartDuringActiveRunIs409`: with a run in progress, flagged start → `409 RUN_IN_PROGRESS` (Review Focus 4; reuse the file's in-progress setup).
  - MCP `StartRunForwardsOnlyNew`: `StartRunAsync(profile, browser, enrich: true, onlyNew: true, Ct)` → `Assert.True(Assert.Single(launcher.Started).OnlyNew)`; mirror default-false via the existing three-arg calls (they must still compile and pass unchanged).
- [ ] **Step 2: Run to verify they fail.** Run from `src/backend`: `dotnet test --project JobScrape.Api.Tests --filter-class "*RunsEndpointTests*"` and `dotnet test --project JobScrape.Mcp.Tests --filter-class "*RunActionsToolsTests*"`. Expected: FAIL on the new tests (flag unbound / `OnlyNew` false on response), pre-existing tests green.
- [ ] **Step 3: Implement.** Model binding carries `onlyNew` with no code (record member); update the controller/start XML docs; MCP adds optional `bool onlyNew = false` before `cancellationToken` (CA1068 keeps the token last; repo treats warnings as errors) and forwards it; existing positional call sites pass the token named.
- [ ] **Step 4: Run to verify they pass.** Run from `src/backend`: `dotnet test --project JobScrape.Api.Tests` and `dotnet test --project JobScrape.Mcp.Tests`. Expected: PASS.
- [ ] **Step 5: Commit.** `feat(runs)!: expose onlyNew on the API and MCP start_run`.

### Task 6: Deck start form and run detail

**Files:**
- Modify: `src/frontend/src/client/lib/api.ts` (`StartRunInput.onlyNew: boolean`, `RunPayload.onlyNew: boolean`), `src/frontend/src/client/pages/runs.tsx` (checkbox, body field, badge, postings note), `src/frontend/src/tests/client/runs.test.tsx` (body exact-match update to include `onlyNew: false`, `baseRun` gains `onlyNew: false`, new assertions)

**Interfaces:**
- Consumes: Task 5 (API shape).
- Produces: working checkbox + badge — used by Tasks 8, 10.

- [ ] **Step 1: Write the failing tests.** In `runs.test.tsx`:
  - Update the POST-body `toEqual` to `{ profileId: "p1", browserProfileId: "s1", enrich: true, onlyNew: false }` (this fails once the form sends the field... order: first add a test `sends onlyNew true when checked` asserting the intercepted body; both fail pre-implementation).
  - `renders the NEW ONLY checkbox unchecked by default` (getByRole checkbox name `NEW ONLY`, `not.toBeChecked()`).
  - `badges a flagged run and notes filtered postings` (mock run payload with `onlyNew: true` → badge text visible; postings note names first-seen-in-window).
- [ ] **Step 2: Run to verify they fail.** Run: `bun test src/frontend/src/tests/client/runs.test.tsx`. Expected: FAIL.
- [ ] **Step 3: Implement.** `api.ts` types; form checkbox `<input type="checkbox" name="onlyNew" aria-label="New only" /> NEW ONLY` (label copy: `NEW ONLY`); `start()` sends `onlyNew: data.get("onlyNew") !== null`; detail shows badge when `selected.onlyNew`; postings module note for flagged runs.
- [ ] **Step 4: Run to verify they pass.** Run: `bun test src/frontend/src/tests/client/runs.test.tsx && bun check`. Expected: PASS + clean typecheck.
- [ ] **Step 5: Commit.** `feat(runs)!: add the new-only start option and badge`.

### Task 7: InfoTip component and runs placements

**Files:**
- Create: `src/frontend/src/client/components/info-tip.tsx`
- Modify: `src/frontend/src/client/styles/globals.css` (`.info-tip` circle + tooltip), `src/frontend/src/client/pages/runs.tsx` (placements: NEW ONLY checkbox, enrich checkbox, NEW-ONLY badge, stat readouts, postings note)
- Test: Create `src/frontend/src/tests/client/info-tip.test.tsx`

**Interfaces:**
- Consumes: Task 6 (the controls it annotates).
- Produces: reusable `InfoTip({ text })` — used by Task 10.

- [ ] **Step 1: Write the failing tests.** `renders a circled i button with aria-describedby pointing at the tooltip text`; `tooltip appears on hover and on focus`; `one-line usage copy for the NEW ONLY control exists on the runs page` (render runs page, hover/focus the NEW ONLY tip, assert the explanation text).
- [ ] **Step 2: Run to verify they fail.** Run: `bun test src/frontend/src/tests/client/info-tip.test.tsx`. Expected: FAIL (file missing).
- [ ] **Step 3: Implement.** Circled-`i` button, tooltip on hover/focus only, keyboard reachable, `aria-describedby`, no motion (instant under reduced-motion via existing global null). Copy: one line per placement, usage not prose. Runs placements listed above; other pages untouched (follow-up).
- [ ] **Step 4: Run to verify they pass.** Run: `bun test src/frontend/src/tests/client/ && bun check && bun run format:check`. Expected: PASS.
- [ ] **Step 5: Commit.** `feat(runs)!: add reusable info tooltips to the runs page`.

### Task 8: Visited posting links

**Files:**
- Modify: `src/frontend/src/client/styles/globals.css`, `src/frontend/src/tests/e2e/runs.e2e.ts` (link-color assertion; Task 10 owns the run)

**Interfaces:**
- Consumes: Task 6 (the links).
- Produces: violet visited rule — verified in Task 10.

- [ ] **Step 1: Write the failing expectation.** E2E addition (runs in Task 10): posting link unvisited computed color equals the signal token; manual eye-check for the visited violet in both themes (browsers hide visited state from scripts — no automated color assertion possible, per spec).
- [ ] **Step 2: Implement.** `.data a:visited { color: var(--brand-3-v, ...) }` — exact: dark `#a78bfa`, light `[data-theme="light"] #6d28d9` using the existing `--brand-3` token (dark `--brand-3: #a78bfa`, light `--brand-3: #6d28d9` already in `globals.css`), so the rule is `.tbl-wrap .data a:visited, table.data a:visited { color: var(--brand-3); }` scoped to data tables. No JS, no tracking.
- [ ] **Step 3: Verify.** `bun run build` + eye-check both themes; e2e unvisited-color assertion green in Task 10.
- [ ] **Step 4: Commit.** `feat(runs)!: tint visited posting links violet`.

### Task 9: Topology diagrams

**Files:**
- Modify: `src/frontend/src/client/pages/topology.tsx` (TOP-05 current scrape flow, TOP-06 new-only delta, reusing `diagram.tsx` TONE/`DiagramDefs`/`Legend`)
- Test: Create `src/frontend/src/tests/client/topology.test.tsx`

**Interfaces:**
- Consumes: Tasks 1–5 (behavior it depicts; labels mirror handler/store names).
- Produces: two rendered diagrams — verified in Task 10 e2e.

- [ ] **Step 1: Write the failing tests.** `renders TOP-05 with aria name "Current scrape flow"`; `renders TOP-06 with aria name "New-only run delta"`; `the new-only path uses the focal tone and dropped exits use gray`; `legends cover every tone used`.
- [ ] **Step 2: Run to verify they fail.** Run: `bun test src/frontend/src/tests/client/topology.test.tsx`. Expected: FAIL.
- [ ] **Step 3: Implement.** Two `Module`s, `span-7` + `span-5` (page grid), ≤9 nodes each, orthogonal connectors, short labels, emerald focal path, gray dropped-card exits, `<svg role="img" aria-label="...">`, `Legend` strip per diagram. Copy from the spec's two diagrams; node names mirror code (`UpsertAsync`, `PostingsNew`, `FirstSeenAt >= run start`).
- [ ] **Step 4: Run to verify they pass.** Run: `bun test src/frontend/src/tests/client/topology.test.tsx && bun check`. Expected: PASS.
- [ ] **Step 5: Commit.** `feat(topology)!: diagram the scrape flow and new-only delta`.

### Task 10: End-to-end automation

**Files:**
- Modify: `src/frontend/src/tests/e2e/runs.e2e.ts` (and topology assertions — new spec file if the runner expects one spec per page: follow the `database.e2e.ts` pattern, create `topology.e2e.ts` if no topology spec exists)

**Interfaces:**
- Consumes: Tasks 6–9.
- Produces: green e2e proof. E2E never performs live scrapes (adapters hit real sites); behavior is covered at API level in Task 5.

- [ ] **Step 1: Write the failing tests.**
  - Runs: NEW ONLY checkbox visible and unchecked by default; tooltip appears on hover and focus with the usage copy; intercept `POST /api/v1/runs` on submit → body contains `onlyNew: true` when checked, `false` when not.
  - Topology: TOP-05 and TOP-06 diagrams visible by aria name on `#/topology`.
  - Links: posting title link unvisited computed color equals the signal token (visited violet is eye-verified, Task 8).
- [ ] **Step 2: Run to verify they fail.** Run: `bun run test:e2e` (scoped per playwright config to the touched specs if the runner supports it, else full). Expected: FAIL.
- [ ] **Step 3: Implement.** Test-only changes; no product code in this task unless a failure proves a product bug — then stop, report, and fix under the owning task's tests first (never edit tests to match broken behavior).
- [ ] **Step 4: Run to verify they pass.** Run: full `bun run test:e2e`. Expected: PASS.
- [ ] **Step 5: Commit.** `test(e2e)!: cover new-only start, tooltips, and flow diagrams`.

### Task 11: Docs and diagram profile

**Files:**
- Modify: `docs/03-domain-model.md` (§8.5: the new-only rule), `docs/12-web-cockpit.md` (runs body field, listing filter, badge, TOP-05/06), plus `src/frontend/README.md` and `docs/09-operations-runbook.md` only if grep shows them naming the old behavior
- Create: `~/.diagram-design/profiles/ops-deck.md`, `<repo>/.diagram-design` containing exactly `profile: ops-deck`

**Interfaces:**
- Consumes: all behavior tasks (docs describe what shipped).
- Produces: matching docs; saved skin.

- [ ] **Step 1: Locate every stale line.** Run: `grep -rn "enrich = true\|PostingsNew\|LastSeenAt falls\|VISIT DETAIL" docs/12-web-cockpit.md docs/03-domain-model.md src/frontend/README.md docs/09-operations-runbook.md`. Record hits.
- [ ] **Step 2: Update.** Same voice as surrounding prose; no new promises. Domain model gains the cap-binds-new and filter rules; cockpit gains the body field, badge, diagrams, tooltip, and visited-link notes.
- [ ] **Step 3: Save the profile.** Write the `ops-deck` body from the approved mapping (paper #0b1110/#ffffff ... mono everywhere, system fonts), header with today's date; write the marker. Verify by re-reading both files.
- [ ] **Step 4: Verify.** Run: `bun run format:check` if docs are covered; `git status` shows only intended files.
- [ ] **Step 5: Commit.** `docs(runs)!: document new-only runs, diagrams, and deck skin profile`.

### Task 12: Full verification loop

**Files:** none (evidence only).

- [ ] **Step 1: Run the backend.** Run from `src/backend`: `dotnet test --solution JobScrape.slnx`. Expected: all green, 0 warnings.
- [ ] **Step 2: Run the deck.** Run: `bun test`, `bun check`, `bun run format:check`, `bun run test:e2e`, plus `bun run test:flags` and `bun run test:pretest` (temp-backend matrices must stay green). Expected: all PASS.
- [ ] **Step 3: Run deslop and the pre-commit checks.** Run `/deslop` reread of the full diff; secrets scan (`git diff` shows no keys/tokens); confirm no console.logs, no commented code.
- [ ] **Step 4: Independent review.** Dispatch a fresh review subagent on `opencode-go/deepseek-v4.1-flash#max` for the branch diff (spec compliance + quality), then a `security-reviewer` on the auth-adjacent surface (start endpoint, MCP tool, export). Fix every CRITICAL and HIGH finding under the owning task's tests (RED first); re-run the owning suites. Repeat until both reviewers are clean.
- [ ] **Step 5: Push.** Conventional commits already landed per task; `git fetch origin`, rebase if needed; run `graphify update .` and commit the refreshed graph files (untracked plan/spec docs commit in Task 11); push. Report the branch and the verification evidence (commands + outputs, per numbers-carry-commands).

## Self-review

- Spec coverage: domain flag (T1), request path (T2), cap/refactor/refresh/enrichment/summary (T3), migration/filter/read-model (T4), API+MCP (T5), form+badge (T6), tooltips (T7), visited links (T8), website diagrams (T9), e2e (T10), docs+profile (T11), verification/review/push (T12). Every spec section has a task.
- Step scan: each test step names exact assertions; each implement step names exact signatures and values. The migration scaffold names the exact command; the `FakeLauncher.Started` capture is verified to exist in both test files; the `runs.test.tsx` body `toEqual` at line 262 is called out for update.
- Type consistency: `OnlyNew`/`onlyNew` naming follows each layer's convention (C# PascalCase, JSON/TS camelCase); `RunResponse`/`ScrapeRunSummary` both carry the flag; no function is renamed between tasks.
- Review Focus: all five lines have owning tests in Tasks 3–5.
- Proportion: the plan is long because it spans twelve commits across two stacks with exact assertions; no task contains a function body the tests do not determine.

---

Plan complete and saved to `docs/superpowers/plans/2026-11-09-new-only-runs.md`. Please review the plan. Does it capture what you want? Your standing instruction already supplies the execution method (agents and subagents with role-matched models), so on your approval I will execute subagent-driven: one `poteto-agent` per task on the role model (`feature` → `opencode/inkling-2-small-free#max`, migration/chores → `opencode-go/beam-free#max`), every task review on `opencode-go/deepseek-v4.1-flash#max`, final whole-branch review likewise with a parent-model second pass.
