# Green Unit Tests, Coverage, Format, Push Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make all unit tests green, raise line coverage toward 90-100%, format the whole codebase, update docs accurately, then push.

**Architecture:** Fix in the smallest loop: baseline first, then backend unit fixes, then frontend unit fixes, then coverage gaps, then format, then docs, then verified push. One task ships one green gate.

**Tech Stack:** .NET 11 SDK 11.0.100, xUnit v3 native MTP (`dotnet test --project`), Bun unit tests (`bun test`), no Playwright browsers.

**Spec:** User message 2026-11-17: use `opencode-go/deepseek-v4.1-flash#max` for coding and `opencode-go/muse-spark-1.3-contributor#xhigh` for code review in a subagent loop until everything is green and resolved then push; do not install any tool/library/software/package; do not modify anything outside the current repo folder; update docs as needed accurately; format whole codebase before push; target 90-100% code coverage; avoid any e2e automated Playwright tests needing a browser; unit tests only.

## Global Constraints

- Coding subagents run on `opencode-go/deepseek-v4.1-flash#max`; review subagents run on `opencode-go/muse-spark-1.3-contributor#xhigh`. No unnamed defaults.
- No installs: no brew, no SDK/package/tool installs, no `playwright install`, no new package references. Implicit restore caches only.
- No modifications outside the current repo folder.
- Backend tests: `dotnet test --project <Tests.csproj>` (native MTP, SDK 10+ `global.json` runner). Exclude `[Trait("Category", "Browser")]` tests (need real Chromium, e.g. `JobScrape.Infrastructure.Tests/Browser/BrowserTests.cs:24`). Exclude all frontend `playwright test` e2e.
- `TreatWarningsAsErrors=true`, `net11.0`, SDK `11.0.100`.
- Format whole codebase before push (backend + frontend), then re-verify green.
- Docs updated only where behavior/commands/counts changed; no invented numbers.

## Review Focus

- A browser-dependent test runs in a unit gate and fails for missing Chromium instead of being excluded.
- A source test hits the live network instead of fixtures.
- A secret or token lands in docs, logs, or test output.
- A time-dependent test flakes under load.
- A new test asserts nothing (passes with the fix reverted).

---

### Task 1: Baseline unit gates (no code changes)

**Files:**
- Read: `src/backend/JobScrape.slnx`, `src/backend/*//*.Tests.csproj`, `src/frontend/package.json`, `docs/06-testing-strategy.md`
- Test: none (execution only)

**Interfaces:**
- Consumes: nothing
- Produces: `BASELINE.md` in the SDD workspace: exact commands run, per-project `Passed/Failed/Skipped` counts, full list of failing tests, coverage tool status, formatter status

- [ ] **Step 1: Run backend unit tests excluding browser-gated tests**

Run (native MTP, no browser): `dotnet test --project src/backend/<Each>.Tests.csproj` with an exclusion for `Category=Browser` (xUnit v3 MTP syntax, e.g. `--filter-trait`/`--filter-query`; resolve exact syntax from repo docs/SDK help, do not guess twice).
Expected: a per-project Passed/Failed/Skipped table; `BrowserTests` excluded, zero browser-download attempts.

- [ ] **Step 2: Run frontend unit tests only**

Run: `bun test` (never `playwright test`, never `test:e2e`).
Expected: pass/fail counts recorded; no browser launched.

- [ ] **Step 3: Record formatter and coverage readiness**

Check: backend formatter available without installs; `bun fmt --check`-style frontend check without writes; whether Cobertura can be collected with existing packages (`coverlet.collector`, `Microsoft.Testing.Extensions.CodeCoverage`).
Expected: `BASELINE.md` states what runs green, what fails, and the exact next fix list.

- [ ] **Step 4: Commit nothing (report only)**

Expected: no commits; report returned with the failing-test list by name.

### Task 2: Backend unit fixes to green

**Files:**
- Modify: backend production/test files named in BASELINE.md only
- Test: the failing backend unit tests from Task 1

**Interfaces:**
- Consumes: BASELINE.md failing-test list
- Produces: green backend unit gate excluding `Category=Browser`

- [ ] **Step 1: Write the failing test first (RED) for each fix**

Per TDD: add or tighten the unit test proving the fix, run it, watch it fail for the right reason.
Expected: FAIL with the production cause, not a typo.

- [ ] **Step 2: Implement the minimal fix (GREEN)**

One fix per failing test; no new packages; no behavior changes beyond the fix.
Expected: the named tests pass.

- [ ] **Step 3: Run the full backend unit gate**

Run: all 6 backend test projects with the Task 1 browser exclusion.
Expected: `Passed: N, Failed: 0` per project.

- [ ] **Step 4: Commit**

Expected: one commit per fix cluster, conventional message.

### Task 3: Frontend unit fixes to green

**Files:**
- Modify: `src/frontend/src/**` files named in BASELINE.md only
- Test: `bun test` unit suites (never `playwright test`)

**Interfaces:**
- Consumes: BASELINE.md frontend failures
- Produces: green `bun test` gate

- [ ] **Step 1: Write the failing test first (RED)**

Expected: FAIL for the production cause.

- [ ] **Step 2: Implement the minimal fix (GREEN)**

Expected: `bun test` passes.

- [ ] **Step 3: Run the frontend unit gate**

Run: `bun test`.
Expected: all pass, zero browsers launched.

- [ ] **Step 4: Commit**

Expected: conventional commit.

### Task 4: Coverage toward 90-100% with honest unit tests

**Files:**
- Create/modify: unit tests for uncovered lines/branches found in Cobertura
- Test: new unit tests

**Interfaces:**
- Consumes: green gates from Tasks 2-3
- Produces: Cobertura report + coverage table; every new test verified red-green

- [ ] **Step 1: Collect Cobertura with existing packages only**

Run: repository-compatible coverage collection (existing `coverlet.collector` / MTP coverage extension only; no new tools).
Expected: Cobertura XML on disk; overall line/branch totals recorded.

- [ ] **Step 2: Add unit tests for the top uncovered blocks**

One test per behavior; each watched to fail first. No browser, no network.
Expected: coverage rises; no test asserts mocks or tautologies.

- [ ] **Step 3: Re-collect and reconcile arithmetic**

Covered/valid totals from Cobertura; required lines for 90% = `ceiling(valid * 0.90)`. Name every remaining below-threshold member; stop only at target or diminishing returns with an honest ledger note.
Expected: final totals stated; any shortfall named, not hidden.

- [ ] **Step 4: Commit**

Expected: conventional commit(s).

### Task 5: Format whole codebase, re-verify green

**Files:**
- Modify: formatting only, repo-wide (backend + frontend)

**Interfaces:**
- Consumes: green, covered tree
- Produces: formatted tree, still green

- [ ] **Step 1: Format backend without installs**

Run: available formatter (SDK `dotnet format` or repo-configured equivalent; no tool installs).
Expected: zero formatting diffs remain; build has zero warnings (TreatWarningsAsErrors).

- [ ] **Step 2: Format frontend**

Run: `bun fmt src package.json bunfig.toml`-equivalent check/write cycle per `package.json` scripts.
Expected: `format:check` clean.

- [ ] **Step 3: Re-run both unit gates after formatting**

Run: Task 1 backend + frontend commands.
Expected: still `Failed: 0` everywhere.

- [ ] **Step 4: Commit**

Expected: `style:` commit with formatting only.

### Task 6: Docs accuracy update

**Files:**
- Modify: docs whose commands/counts/behavior changed (e.g. `docs/06-testing-strategy.md`, `README.md` counts, runbook commands)
- Test: none

**Interfaces:**
- Consumes: final commands, counts, coverage totals
- Produces: docs matching the tree; no invented numbers

- [ ] **Step 1: Update only stale docs**

Change only what the work changed: test commands, browser-exclusion rule, coverage totals, counts.
Expected: every stated command runs verbatim; every number matches evidence.

- [ ] **Step 2: Commit**

Expected: `docs:` commit.

### Task 7: Final verification and push

**Files:**
- Modify: none

**Interfaces:**
- Consumes: formatted, documented, green tree
- Produces: pushed `development` branch

- [ ] **Step 1: Run the full verification set**

Run: backend unit gate (browser-excluded), `bun test`, format checks, `git status`, `git diff --check`.
Expected: all green, working tree holds only intended commits.

- [ ] **Step 2: Push**

Run: `git push`.
Expected: remote `development` advances; no hooks bypassed.
