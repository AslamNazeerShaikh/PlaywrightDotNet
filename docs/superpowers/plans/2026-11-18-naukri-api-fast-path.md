# Naukri API Fast Path Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Naukri runs fetch listings through Naukri's `jobapi` endpoints by default, with the Playwright path kept as an explicit opt-out.

**Architecture:** A second `IJobSource` (`NaukriApiJobSource`) behind the same `SourceKey`, chosen by a new per-source `ApiOnly` flag (default true). Token minting, HTTP transport, and JSON row mapping are new; parsers' gate, classifier, delays, caps, and cooldowns are reused untouched.

**Tech Stack:** .NET 11, `IHttpClientFactory` (`Microsoft.Extensions.Http`, already referenced), `System.Security.Cryptography`, `System.Text.Json`, xUnit v3 native MTP, TDD, no new packages.

**Spec:** `docs/superpowers/specs/2026-11-18-naukri-api-fast-path.md` — the plan argues from the spec; executors read both.

## Global Constraints

- `net11.0`, SDK `11.0.100`, `TreatWarningsAsErrors=true`, XML docs on every public member.
- Zero new package references. No `playwright install`, no browsers in unit gates.
- Backend unit gate excludes `Category=Browser`: `dotnet test --project src/backend/JobScrape.<N>.Tests/JobScrape.<N>.Tests.csproj --filter-not-trait "Category=Browser"` from `src/backend/`.
- Never run `playwright test`/e2e. Never touch `src/frontend`, `docs/` (except the task that owns docs), or the plan file.
- Coding subagents run on `opencode-go/deepseek-v4.1-flash#max`; review subagents on `opencode-go/muse-spark-1.3-contributor#xhigh`. No unnamed defaults.
- No TLS impersonation, no proxy rotation, no harvested cookies, no login. Politeness (caps, delays, cooldowns) is the defense.
- MIT attribution comment wherever JobSpy's public key or algorithm is ported.
- TDD: every behavior has a failing-first test; never weaken an existing assertion.

## Review Focus

- Rotated API key (every request `406`, not rate-limiting) must surface distinctly from a plain rate limit, so the operator knows to wait for a key update rather than just cool down. Pinned in Task 4 by `StopsRunAndNamesKeyRotationAfterThreeConsecutive406s`.
- Time-bound tokens must never be replayed or cached across requests. Pinned in Task 2 by `TwoTokensForTheSamePageDiffer` and `TokenDecodesToKeySize`.
- A `3xx` redirect must never be followed blindly (redirect traps). Pinned in Task 3 by `RedirectResponseIsClassifiedAsWallNotFollowed` (client built with auto-redirect off).
- The page loop must be bounded even when every page is full (no runaway requests). Pinned in Task 3 by `StopsAtReportedTotalAndAtMaxJobsWithBoundedRequestCount`.
- Company-portal rows (`companyApplyJob`/`clientCareersUrl`/`applyRedirectUrl`) must be flagged external, never silently collected as normal postings. Pinned in Task 3 by `ExternalPortalRowsAreMarkedExternal`.

---

### Task 1: `ApiOnly` source option (default true)

**Files:**
- Modify: `src/backend/JobScrape.Infrastructure/Configuration/SourceOptions.cs`
- Modify: `src/backend/JobScrape.Api/appsettings.json` (Naukri section)
- Test: `src/backend/JobScrape.Infrastructure.Tests/Configuration/OptionsTests.cs` (existing file, add cases)

**Interfaces:**
- Consumes: nothing
- Produces: `SourceOptions.ApiOnly { get; init; } = true` read by Task 4; validator rule (if any) both tasks honor.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void ApiOnlyDefaultsToTrue() // new SourceOptions().ApiOnly is true
[Fact] public void ApiOnlyBindsFalseFromConfiguration() // "ApiOnly": false binds
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --project src/backend/JobScrape.Infrastructure.Tests/JobScrape.Infrastructure.Tests.csproj --filter-class "*OptionsTests*" --filter-not-trait "Category=Browser"` from `src/backend/`
Expected: FAIL (no such property)

- [ ] **Step 3: Implement `ApiOnly` in `SourceOptions.cs`**

Plain bool, default `true`, XML doc stating default-on means the API path and `false` restores the browser path. No validation rule (both positions legal).

- [ ] **Step 4: Set the Naukri default in `appsettings.json`**

Explicit `"ApiOnly": true` on the Naukri section so the default is visible to operators, not just code.

- [ ] **Step 5: Run tests to verify they pass**

Run: same command as Step 2
Expected: PASS, whole class green

- [ ] **Step 6: Commit**

```bash
git add src/backend/JobScrape.Infrastructure/Configuration/SourceOptions.cs src/backend/JobScrape.Api/appsettings.json src/backend/JobScrape.Infrastructure.Tests/Configuration/OptionsTests.cs
git commit -m "feat(sources): add ApiOnly transport switch defaulting to API"
```

### Task 2: `nkparam` token minter

**Files:**
- Create: `src/backend/JobScrape.Infrastructure/Sources/Naukri/NaukriRequestToken.cs`
- Test: `src/backend/JobScrape.Infrastructure.Tests/Sources/Naukri/NaukriRequestTokenTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces: `NaukriRequestToken.Generate(string page, RSA parameters)` (exact shape: implementer chooses static vs injectable, but tests must inject a throwaway key) and the embedded JobSpy public key constant with MIT attribution; Task 3 calls it per request, never caching the output.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void TokenDecodesToKeySize() // base64 decodes to modulus byte length
[Fact] public void TwoTokensForTheSamePageDiffer() // randomized padding, never replayed
[Fact] public void AcceptsArbitraryPageTag() // "srp" and a job id both work
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --project src/backend/JobScrape.Infrastructure.Tests/JobScrape.Infrastructure.Tests.csproj --filter-class "*NaukriRequestTokenTests*" --filter-not-trait "Category=Browser"` from `src/backend/`
Expected: FAIL (type does not exist)

- [ ] **Step 3: Implement the minter in `NaukriRequestToken.cs`**

RSA PKCS#1 v1.5 of `v0|<epoch-ms>|121_<page>` via `System.Security.Cryptography`. Key injectable for tests; production constant is JobSpy's with MIT attribution. No caching of outputs.

- [ ] **Step 4: Run tests to verify they pass**

Run: same command as Step 2
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/backend/JobScrape.Infrastructure/Sources/Naukri/NaukriRequestToken.cs src/backend/JobScrape.Infrastructure.Tests/Sources/Naukri/NaukriRequestTokenTests.cs
git commit -m "feat(naukri): mint per-request nkparam tokens"
```

### Task 3: API search client (fixtures only, no network)

**Files:**
- Create: `src/backend/JobScrape.Infrastructure/Sources/Naukri/NaukriApiClient.cs`
- Test: `src/backend/JobScrape.Infrastructure.Tests/Sources/Naukri/NaukriApiClientTests.cs` (+ fixture JSON)

**Interfaces:**
- Consumes: Task 2 token generator (call per request, never cache).
- Produces: search-page fetch returning `(rows, total)` with status mapping (`200` parse, `400` clean end, `406` rate-limited, else transient, `3xx` wall); row mapping through the existing parser gate (bad rows drop with warning); external-portal flag from `companyApplyJob`/`clientCareersUrl`/`applyRedirectUrl`. Task 4 drives paging and detail through this client.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void MapsSearchRowsThroughTheSharedGate() // tagsAndSkills, company, salary, work mode from fixture
[Fact] public void BadRowsDropWithWarningInsteadOfPoisoning() // malformed row skipped
[Fact] public void ExternalPortalRowsAreMarkedExternal()
[Fact] public void Http400MeansCleanEndNotError()
[Fact] public void Http406MapsToRateLimited()
[Fact] public void RedirectResponseIsClassifiedAsWallNotFollowed()
[Fact] public void StopsAtReportedTotalAndAtMaxJobsWithBoundedRequestCount()
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --project src/backend/JobScrape.Infrastructure.Tests/JobScrape.Infrastructure.Tests.csproj --filter-class "*NaukriApiClientTests*" --filter-not-trait "Category=Browser"` from `src/backend/`
Expected: FAIL (type does not exist)

- [ ] **Step 3: Implement `NaukriApiClient`**

`IHttpClientFactory` named client, auto-redirect OFF, honest UA, `appid: 109`/`systemid: Naukri` headers, fresh token per call, 15–30 s timeout. Fixture JSON in tests; fake `HttpMessageHandler`; zero live calls.

- [ ] **Step 4: Run tests to verify they pass**

Run: same command as Step 2
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/backend/JobScrape.Infrastructure/Sources/Naukri/NaukriApiClient.cs src/backend/JobScrape.Infrastructure.Tests/Sources/Naukri/NaukriApiClientTests.cs
git commit -m "feat(naukri): fetch search pages through the jobapi client"
```

### Task 4: Adapter wiring (flag select, detail, walls)

**Files:**
- Create: `src/backend/JobScrape.Infrastructure/Sources/Naukri/NaukriApiJobSource.cs`
- Modify: registry selection for `SourceKey.Naukri` (implementer locates: `BrowserRegistration.cs` / `JobSourceRegistry`)
- Test: `src/backend/JobScrape.Infrastructure.Tests/Sources/Naukri/NaukriApiJobSourceTests.cs`

**Interfaces:**
- Consumes: Task 1 `ApiOnly`, Task 3 client (paging + detail via `appid: 121` + `nkparam(jobId)`).
- Produces: flag-on runs use the API source end to end; flag-off behavior byte-identical (existing suite proves it); `406` ends the run with partials kept.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void FlagOnSelectsApiSourceForNaukri()
[Fact] public void FlagOffKeepsBrowserSourceUnchanged()
[Fact] public void Single406EndsRunWithPartialsKept()
[Fact] public void StopsRunAndNamesKeyRotationAfterThreeConsecutive406s()
[Fact] public void DetailEnrichesThroughJobApiV4()
[Fact] public void InterPagePauseUsesConfiguredDelays()
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --project src/backend/JobScrape.Infrastructure.Tests/JobScrape.Infrastructure.Tests.csproj --filter-class "*NaukriApiJobSourceTests*" --filter-not-trait "Category=Browser"` from `src/backend/`
Expected: FAIL (type does not exist)

- [ ] **Step 3: Implement `NaukriApiJobSource` + registry selection**

Same `IJobSource` contract (`SourceKey.Naukri`); page loop with the four stop conditions and `jobId` dedupe; detail enrichment via the client; inter-page pause through `IDelay`; `406`/non-200 mapped per spec; no browser objects touched on the flag-on path.

- [ ] **Step 4: Run the full backend gate to verify nothing regressed**

Run: all six test projects with `--filter-not-trait "Category=Browser"` from `src/backend/` (Architecture without `--coverage`)
Expected: `Failed: 0` everywhere

- [ ] **Step 5: Commit**

```bash
git add src/backend/JobScrape.Infrastructure/Sources/Naukri/ src/backend/JobScrape.Infrastructure.Tests/Sources/Naukri/NaukriApiJobSourceTests.cs
git commit -m "feat(naukri): serve runs from the API source by default"
```

### Task 5: Docs, format, verify

**Files:**
- Modify: `docs/08-site-playbooks.md` (Naukri section), `docs/09-operations-runbook.md` (flag + 406 guidance), `docs/README.md` or `docs/21-feature-toggles.md` (whichever documents source options)
- Test: none (docs only; gates re-run as verification)

**Interfaces:**
- Consumes: Tasks 1–4 behavior.
- Produces: docs matching the tree; formatted tree; green gates.

- [ ] **Step 1: Update the three docs**

Flag name, default, opt-out; `406` meaning (rate-limit vs key rotation) and operator response; no invented numbers.

- [ ] **Step 2: Format the touched projects**

Run: `dotnet format src/backend/JobScrape.slnx --verify-no-changes --no-restore` from repo root; if dirty, run `dotnet format` on touched projects and re-verify
Expected: exit 0, no output

- [ ] **Step 3: Re-run the full backend gate**

Run: all six test projects with `--filter-not-trait "Category=Browser"` from `src/backend/`
Expected: `Failed: 0` everywhere

- [ ] **Step 4: Commit**

```bash
git add docs/08-site-playbooks.md docs/09-operations-runbook.md docs/README.md
git commit -m "docs(naukri): document the API fast path and its flag"
```
(Adjust file list to the docs actually touched.)
