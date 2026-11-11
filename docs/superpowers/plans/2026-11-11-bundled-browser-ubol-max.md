# Bundled-only browsers + uBOL max Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship bundled-only Playwright browsers with uBOL max for Naukri and zero system Chrome dependence.

**Architecture:** Delete branded channel paths, keep one-member `Bundled` channel with data migration, switch e2e to bundled Chromium, scope uBOL max to headed bundled Chromium, rewrite docs and screenshot tool.

**Tech Stack:** Microsoft.Playwright 1.64.1 (backend), @playwright/test 1.65.0 (frontend), .NET 10, xUnit v3 (MTP runner via `dotnet run`), EF Core with SQLite, uBOLite 2026.914.1325.

**Spec:** `docs/superpowers/specs/2026-11-11-bundled-browser-ubol-max-design.md`

## Global Constraints

- Microsoft.Playwright stays 1.64.1. @playwright/test stays ^1.65.0.
- Committed `appsettings.json` keeps `AdblockMode: null` and `ExtensionDir: null`.
- `Headless` default stays `false`.
- Never edit applied EF migrations. New migration is data-only SQL.
- Never reuse deleted `BrowserChannel` ordinals.
- `vendor/ubol-chrome-max` stays gitignored. Only `vendor/download-ubol.sh` is tracked.
- TDD every production change: failing test first, watch it fail, minimal code, full suite green.
- Conventional Commits. One logical change per commit.

## Review Focus

- Stored `Chrome` or `Edge` row still reads without `Enum.Parse` throw and shows as `Bundled`.
- `channel: "Chrome"` payload with `Chromium` engine now fails validation with `Choose Bundled.`
- Headless with `ubol-max` set still launches without extension flags and without error.
- Missing `ExtensionDir` with mode set still fails host startup naming `vendor/download-ubol.sh`.
- Frontend e2e with no system Chrome still launches `chromium-1254` and passes.

---

### Task 1: Frontend e2e on bundled Chromium

**Files:**
- Modify: `src/frontend/playwright.config.ts`
- Modify: `src/frontend/README.md`
- Test: `src/frontend/src/tests/e2e/` suite via `bun run test:e2e`

**Interfaces:**
- Consumes: @playwright/test 1.65.0 with `chromium-1254` at `~/Library/Caches/ms-playwright`.
- Produces: e2e project named `chromium` with no `channel` key. Later tasks assume e2e needs no system Chrome.

- [ ] **Step 1: Write the failing check**

```bash
ls "/Applications/Google Chrome.app"
# Expected: No such file or directory (already true after brew uninstall)
```

- [ ] **Step 2: Run e2e to verify it fails on channel**

Run: `bun run test:e2e` from `src/frontend`
Expected: FAIL with `Chromium distribution 'chrome' is not found`.

- [ ] **Step 3: Implement `playwright.config.ts` bundled project in `src/frontend/playwright.config.ts`**

Delete `channel: "chrome"` line. Project becomes `{ name: "chromium", use: {} }`. Reword header comment lines 9-12 to bundled Chromium with `bunx playwright install chromium` prerequisite.

- [ ] **Step 4: Run e2e to verify it passes**

Run: `bun run test:e2e` from `src/frontend`
Expected: PASS (12 passed).

- [ ] **Step 5: Update `src/frontend/README.md` lines 41, 54-59 to bundled Chromium plus install command**

- [ ] **Step 6: Commit**

```bash
git add src/frontend/playwright.config.ts src/frontend/README.md
git commit -m "feat(e2e): use bundled Chromium instead of system Chrome"
```

### Task 2: Backend bundled-only channels with migration

**Files:**
- Modify: `src/backend/JobScrape.Domain/Access/BrowserChannel.cs`
- Modify: `src/backend/JobScrape.Domain/Access/BrowserEngine.cs` doc
- Modify: `src/backend/JobScrape.Domain/Access/BrowserProfile.cs`
- Modify: `src/backend/JobScrape.Application/Features/Sessions/CreateSessionHandler.cs`
- Modify: `src/backend/JobScrape.Application/Features/Sessions/UpdateSessionHandler.cs`
- Modify: `src/backend/JobScrape.Infrastructure/Browser/BrowserSessionPool.cs`
- Modify: `src/frontend/src/client/pages/sessions.tsx`
- Modify: `src/frontend/src/client/lib/api.ts`
- Modify: `src/frontend/src/tests/client/runs.test.tsx`
- Modify: `src/frontend/src/tests/client/info-tip.test.tsx`
- Modify: `src/frontend/src/tests/e2e/runs.e2e.ts` fixture
- Modify: `src/backend/JobScrape.Domain.Tests/Access/BrowserProfileTests.cs`
- Modify: `src/backend/JobScrape.Application.Tests/SessionHandlersTests.cs`
- Modify: `src/backend/JobScrape.Application.Tests/UpdateSessionHandlerTests.cs`
- Modify: `src/backend/JobScrape.Infrastructure.Tests/Persistence/PersistenceTests.cs`
- Modify: `src/backend/JobScrape.Api.Tests/SessionsEndpointTests.cs`
- Create: `src/backend/JobScrape.Infrastructure/Migrations/<timestamp>_MigrateChannelsToBundled.cs`
- Test: `dotnet run --project` per test csproj (MTP runner, not bare `dotnet test`)

**Interfaces:**
- Consumes: Task 1 needs no input. This task consumes nothing from Task 1.
- Produces: `BrowserChannel.Bundled` only. Channel error text `Choose Bundled.` Later docs tasks reference this end state.

- [ ] **Step 1: Write the failing test for domain single channel**

```csharp
Assert.Equal(new[] { "Bundled" }, Enum.GetNames(typeof(BrowserChannel)));
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project src/backend/JobScrape.Domain.Tests/JobScrape.Domain.Tests.csproj -- -class JobScrape.Domain.Tests.Access.BrowserProfileTests`
Expected: FAIL on names assertion (3 vs 1).

- [ ] **Step 3: Implement `BrowserChannel` Bundled-only in `src/backend/JobScrape.Domain/Access/BrowserChannel.cs`**

Delete `Chrome` and `Edge` members. Update XML docs. Update `BrowserEngine.cs` doc line 9 and `BrowserProfile.cs` `EnsureValidEngineChannel` to drop brand-pairing branch (keep `Enum.IsDefined` guard).

- [ ] **Step 4: Run domain tests to verify they pass**

Run: same domain command plus updated `BrandedChannelsRequireChromium` rewritten to Bundled-only assertions.
Expected: PASS (47 passed).

- [ ] **Step 5: Implement handlers `CreateSessionHandler.cs` and `UpdateSessionHandler.cs` Bundled-only validation**

Channel error becomes `Choose Bundled.` Delete brand-pairing `else if`. Update XML docs on request DTOs.

- [ ] **Step 6: Implement `BrowserSessionPool.cs` always-bundled launch**

Delete `Channel()` method. Drop `Channel =` from `LaunchPersistentContextOptions`. Keep `ExtensionArgs` unchanged.

- [ ] **Step 7: Implement frontend `sessions.tsx`, `api.ts`, fixtures to `Bundled`**

`CHANNELS = ["Bundled"]`. `SessionPayload.channel: "Bundled"`. Fixtures in `runs.test.tsx:243`, `info-tip.test.tsx:106`, `runs.e2e.ts:45` become `Bundled`.

- [ ] **Step 8: Implement EF data-only migration `UPDATE browser_profiles SET channel = 'Bundled' WHERE channel IN ('Chrome','Edge')` with no-op `Down`**

- [ ] **Step 9: Run full backend suites to verify green**

Run: `dotnet run --project` for Domain, Application, Infrastructure, Api Tests csprojs.
Expected: PASS all.

- [ ] **Step 10: Commit**

```bash
git add src/backend src/frontend
git commit -m "feat(browser): support bundled Chromium only"
```

### Task 3: uBOL max for Naukri stays headed bundled-only

**Files:**
- Modify: `docs/07-anti-bot-and-compliance.md`
- Test: `src/backend/JobScrape.Infrastructure.Tests/Browser/BrowserExtensionArgsTests.cs` (existing, no production change)

**Interfaces:**
- Consumes: Task 2 bundled-only launch. No new launch args.
- Produces: Naukri posture documented as headed bundled Chromium plus `ubol-max`. Docs tasks consume this wording.

- [ ] **Step 1: Write the failing doc check**

```bash
grep -n "uBlock Origin Lite.*Optional" docs/07-anti-bot-and-compliance.md
# Expected: row says Optional — must change for Naukri max requirement
```

- [ ] **Step 2: Run extension-args tests to verify green baseline**

Run: `dotnet run --project src/backend/JobScrape.Infrastructure.Tests/JobScrape.Infrastructure.Tests.csproj -- -class JobScrape.Infrastructure.Tests.Browser.BrowserExtensionArgsTests`
Expected: PASS (5 passed).

- [ ] **Step 3: Update `docs/07-anti-bot-and-compliance.md` section 5.1 and section 6.1 rows for Naukri max**

State: Naukri uses headed bundled Chromium with `ubol-max`. Keep committed default `null`. Keep local opt-in path. Change Extensions row from `None` to Naukri max requirement. Delete `chrome-stable/` optional tree entry.

- [ ] **Step 4: Commit**

```bash
git add docs/07-anti-bot-and-compliance.md
git commit -m "docs(anti-bot): require uBOL max for Naukri on bundled Chromium"
```

### Task 4: Docs and screenshot tool bundled-only

**Files:**
- Modify: `docs/09-operations-runbook.md`
- Modify: `docs/04-solution-and-tooling.md`
- Modify: `docs/02-architecture.md`
- Modify: `docs/03-domain-model.md`
- Modify: `docs/00-executive-summary.md`
- Modify: `docs/README.md`
- Modify: `docs/11-open-decisions.md`
- Modify: `tools/screenshots/capture.ts`
- Modify: `tools/screenshots/README.md`
- Test: `grep -rn` for banned literals in `src/` and `vendor/*.sh`

**Interfaces:**
- Consumes: Tasks 1-3 end states.
- Produces: Zero system-Chrome references outside historical research doc and applied migrations.

- [ ] **Step 1: Write the failing grep check**

```bash
grep -rn "channel.*Chrome\|Google Chrome on PATH\|msedge" src/ vendor/*.sh docs/09-operations-runbook.md docs/04-solution-and-tooling.md tools/screenshots/ | grep -v "01-research-analysis"
# Expected: hits found (must become empty)
```

- [ ] **Step 2: Implement docs rewrite per spec fragment section 4 tables plus darwin-arm64 install path**

Add `node/darwin-arm64/node .../cli.js install chromium`. Delete `Chrome/Edge` skip paths. Mark `JobLens.Cli` refs as design target where present.

- [ ] **Step 3: Implement `tools/screenshots/capture.ts` bundled Chromium CDP launch**

Resolve bundled binary under `~/Library/Caches/ms-playwright` (or `PLAYWRIGHT_BROWSERS_PATH`) instead of `/Applications/Google Chrome.app`.

- [ ] **Step 4: Run grep to verify clean**

Run: same grep command.
Expected: no output.

- [ ] **Step 5: Commit**

```bash
git add docs tools
git commit -m "docs(browsers): document bundled-only Chromium and uBOL max"
```

### Task 5: Format, graphify, verify, push

**Files:**
- Touch: whole tree via formatters (no logic change)

**Interfaces:**
- Consumes: Tasks 1-4 commits.
- Produces: Clean tree ready for `development` remote push.

- [ ] **Step 1: Format backend and frontend**

Run: `dotnet format` from `src/backend` then `bun run format` or `bunx prettier --write` from `src/frontend` per repo (verify with repo README).
Expected: no diff after second run.

- [ ] **Step 2: Run full verification**

Run: backend `dotnet run` suites plus `bun run test:e2e` plus `bun run typecheck` or equivalent.
Expected: all green.

- [ ] **Step 3: Update graph**

Run: `graphify update .`
Expected: `graphify-out/` refreshed.

- [ ] **Step 4: Push to development**

Run: `git push origin development`
Expected: remote updated.
