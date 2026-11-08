# Feature flags foundation + small switches Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the `JobScrape:Features` options section, an anonymous capability endpoint, and the logging, health, CSV, and Excel toggles, backend only.

**Architecture:** One new options class bound and validated at the composition root, mapped once to a plain `FeatureFlags` record for the application layer. Each toggle gates exactly one registration or route. The capability endpoint exposes the five flags anonymously for the UI plan to consume.

**Tech Stack:** .NET 10, ASP.NET Core options + ValidateOnStart, xUnit, MTP test runner.

**Spec:** `docs/superpowers/specs/2026-11-08-feature-toggles-design.md`

## Global Constraints

- Flags bind once at host startup and stay immutable per process.
- Fail closed: bad values fail the host via ValidateOnStart, never one request.
- Every default preserves current behavior: `Authentication true, Logging true, Health true, CsvExport true, ExcelExport true`.
- Application layer takes the plain `FeatureFlags` record, never `IOptions<T>`.
- No new packages.

## Review Focus

- Disabled export format answers 404 feature-disabled, not 400: a client distinguishing "bad format" from "turned off" expects the 404. Pinned in Task 5.
- Format-less export with CSV off resolves to XLSX, not a 400: the controller default must follow the flags. Pinned in Task 5.
- Logging off with `Logging:Directory` deleted still boots: validation is conditional on the flag. Pinned in Task 3.
- MCP `get_health` stays alive when the HTTP route is off: the split is deliberate and a test pins both halves. Pinned in Task 4.
- The capability endpoint answers anonymously under the default flags: the UI cannot boot without it. Pinned in Task 2.

---

## File structure

- `src/backend/JobScrape.Infrastructure/Configuration/FeaturesOptions.cs` (create): five bool init props defaulting true, `SectionName = "JobScrape:Features"`.
- `src/backend/JobScrape.Application/Abstractions/FeatureFlags.cs` (create, exact namespace to mirror `ICallerContext.cs` neighbors): plain record `FeatureFlags(bool Authentication, bool Logging, bool Health, bool CsvExport, bool ExcelExport)`.
- `src/backend/JobScrape.Infrastructure/Composition/OptionsRegistration.cs` (modify): bind + ValidateDataAnnotations + ValidateOnStart, map `FeatureFlags` singleton from `IOptions<FeaturesOptions>`.
- `src/backend/JobScrape.Api/appsettings.json` (modify): `"Features"` object with the five true defaults.
- `src/backend/JobScrape.Api/Controllers/FeaturesController.cs` (create): anonymous `GET /api/v1/features` returning the five flags.
- `src/backend/JobScrape.Infrastructure/Composition/LoggingRegistration.cs` (modify): skip sink registration when `Logging` is false.
- `src/backend/JobScrape.Api/Controllers/HealthController.cs` (modify): 404 when `Health` is false.
- `src/backend/JobScrape.Infrastructure/Composition/UseCaseRegistration.cs` (modify): conditional writer factories.
- `src/backend/JobScrape.Application/Features/Runs/ExportRunPostingsHandler.cs` (modify): 404 when the requested format is disabled, dynamic error text.
- `src/backend/JobScrape.Api/Controllers/RunsController.cs` (modify): nullable `format` query, flag-driven default.
- Tests live next to existing ones: `JobScrape.Infrastructure.Tests/Configuration/OptionsTests.cs`, `JobScrape.Api.Tests/OptionsStartupTests.cs`, `JobScrape.Api.Tests/FeaturesEndpointTests.cs` (create), plus rows in `HealthEndpointTests.cs`, `RunsEndpointTests.cs`, `ExportRunPostingsHandlerTests.cs`.

## Task 1: Options, record, registration, shipped keys

**Files:**
- Create: `src/backend/JobScrape.Infrastructure/Configuration/FeaturesOptions.cs`
- Create: `src/backend/JobScrape.Application/Abstractions/FeatureFlags.cs`
- Modify: `src/backend/JobScrape.Infrastructure/Composition/OptionsRegistration.cs`
- Modify: `src/backend/JobScrape.Api/appsettings.json`
- Test: `src/backend/JobScrape.Infrastructure.Tests/Configuration/OptionsTests.cs`

**Interfaces:**
- Consumes: nothing (first task).
- Produces: `FeaturesOptions.SectionName = "JobScrape:Features"`; `FeatureFlags` record; `IOptions<FeaturesOptions>` + `FeatureFlags` resolvable from DI.

- [ ] **Step 1: Write the failing test.** In `OptionsTests.cs`, add `FeaturesDefaultsAreAllTrue` asserting a fresh `FeaturesOptions` has all five flags true, and `FeaturesInvalidValuesFail` is not needed (plain bools need no validation test; instead assert `ValidateDataAnnotations` passes on defaults via the existing options-validation helper pattern used by neighboring tests).
- [ ] **Step 2: Run it to verify it fails.** Run: `dotnet test JobScrape.Infrastructure.Tests --filter "FullyQualifiedName~FeaturesDefaultsAreAllTrue"` from `src/backend`. Expected: FAIL, type not found.
- [ ] **Step 3: Implement.** Create `FeaturesOptions` (five `bool` init props defaulting `true`, `SectionName` const) and the `FeatureFlags` record. Register bind + ValidateDataAnnotations + ValidateOnStart plus the `FeatureFlags` singleton mapped from `IOptions<FeaturesOptions>`, following the `ComplianceOptions`/`ComplianceConsent` pair at `OptionsRegistration.cs:36-39,66-77`. Add the five true keys under `"Features"` in `appsettings.json`.
- [ ] **Step 4: Run the tests.** Same command. Expected: PASS. Then `dotnet build JobScrape.slnx` with 0 warnings, 0 errors.
- [ ] **Step 5: Commit.** `git add` the five files. Message: `feat(config): add JobScrape Features flags section`.

## Task 2: Anonymous capability endpoint

**Files:**
- Create: `src/backend/JobScrape.Api/Controllers/FeaturesController.cs`
- Test: `src/backend/JobScrape.Api.Tests/FeaturesEndpointTests.cs`

**Interfaces:**
- Consumes: `FeatureFlags` singleton from Task 1.
- Produces: `GET /api/v1/features` anonymous JSON shape `{authentication, logging, health, csvExport, excelExport}` (camelCase via the default JSON policy).

- [ ] **Step 1: Write the failing tests.** `FeaturesEndpoint_ReturnsShippedDefaults` (no auth header; assert all five true) and `FeaturesEndpoint_ReflectsOverride` (ApiFactory `UseSetting("JobScrape:Features:Health", "false")`, assert `health` false, others true).
- [ ] **Step 2: Run to verify they fail.** `dotnet test JobScrape.Api.Tests --filter "FullyQualifiedName~FeaturesEndpoint"`. Expected: FAIL, 404 (no route).
- [ ] **Step 3: Implement `FeaturesController`.** `[ApiController]`, `[Route("api/v1/features")]`, `[AllowAnonymous]`, single `[HttpGet]` returning the record. Constructor takes `FeatureFlags`. Follow `HealthController.cs` attribute shape.
- [ ] **Step 4: Run to verify.** Same command. Expected: PASS, 2/2.
- [ ] **Step 5: Commit.** Message: `feat(api): expose anonymous features capability endpoint`.

## Task 3: Logging toggle

**Files:**
- Modify: `src/backend/JobScrape.Infrastructure/Composition/LoggingRegistration.cs`
- Modify: `src/backend/JobScrape.Infrastructure/Configuration/LoggingOptions.cs` (only if validation needs it — see below)
- Test: `src/backend/JobScrape.Infrastructure.Tests/Composition/LoggingRegistrationTests.cs`

**Interfaces:**
- Consumes: `FeatureFlags` singleton.
- Produces: no sinks registered when `Logging` is false; Serilog itself still registered.

- [ ] **Step 1: Write the failing test.** `LoggingOffRegistersNoSinks`: build services with `Options.Create(new FeaturesOptions { Logging = false, ...defaults })`, resolve the logger configuration path the existing `WritesBothSinksFromOneEvent` test uses, emit a marker, dispose, assert no `.jsonl` and no `.log` files appear in the temp dir.
- [ ] **Step 2: Run to verify it fails.** `dotnet test JobScrape.Infrastructure.Tests --filter "FullyQualifiedName~LoggingOffRegistersNoSinks"`. Expected: FAIL (files written).
- [ ] **Step 3: Implement.** In `AddJobScrapeLogging`, resolve `FeatureFlags` and skip both `AddAsyncFileSink` calls plus the console sink when `Logging` is false. Keep `AddSerilog`, enrichers, and disposal untouched. Keep `LoggingOptions.Directory` required: shipped defaults always carry it, and deleting it while disabling logging is an operator error the startup failure correctly surfaces.
- [ ] **Step 4: Run.** Same filter plus the existing `WritesBothSinksFromOneEvent`. Expected: PASS.
- [ ] **Step 5: Commit.** Message: `feat(logging): gate sinks on Features Logging flag`.

## Task 4: Health toggle, route only

**Files:**
- Modify: `src/backend/JobScrape.Api/Controllers/HealthController.cs`
- Test: `src/backend/JobScrape.Api.Tests/HealthEndpointTests.cs` (rows) + MCP health test file that covers `get_health` (assert unchanged — name it in the report).

**Interfaces:**
- Consumes: `FeatureFlags`.
- Produces: `GET /api/v1/health` 404 when `Health` is false; MCP `get_health` still answers.

- [ ] **Step 1: Write the failing test.** `HealthEndpoint_RespectsFlag`: ApiFactory `UseSetting("JobScrape:Features:Health", "false")`, `GET /api/v1/health` asserts 404. Keep the existing 200 test untouched (default true).
- [ ] **Step 2: Run to verify it fails.** `dotnet test JobScrape.Api.Tests --filter "FullyQualifiedName~HealthEndpoint_RespectsFlag"`. Expected: FAIL (200).
- [ ] **Step 3: Implement.** Inject `FeatureFlags` into `HealthController`; first line of the action returns the feature-disabled 404 problem when the flag is false. Touch nothing in `HealthTools` or `GetHealthHandler`.
- [ ] **Step 4: Run.** Same filter + full `HealthEndpointTests` + the MCP health test. Expected: PASS.
- [ ] **Step 5: Commit.** Message: `feat(health): gate HTTP route on Features Health flag`.

## Task 5: Export toggles

**Files:**
- Modify: `src/backend/JobScrape.Infrastructure/Composition/UseCaseRegistration.cs`
- Modify: `src/backend/JobScrape.Application/Features/Runs/ExportRunPostingsHandler.cs`
- Modify: `src/backend/JobScrape.Api/Controllers/RunsController.cs` (`ExportAsync`)
- Test: `src/backend/JobScrape.Api.Tests/RunsEndpointTests.cs` (rows), `ExportRunPostingsHandlerTests.cs` (rows)

**Interfaces:**
- Consumes: `FeatureFlags`.
- Produces: disabled format answers 404; format-less export defaults to CSV, else XLSX, else 404.

- [ ] **Step 1: Write the failing tests.** Endpoint: `ExportXlsx_RespectsFlag` (`UseSetting CsvExport/Xlsx` off → 404) for each format, and `ExportDefault_FallsBackToXlsxWhenCsvOff`. Handler: writer-missing case asserts the disabled-format error (extend the existing writer-missing test to set the flag).
- [ ] **Step 2: Run to verify they fail.** `dotnet test` with the new test names. Expected: FAIL (200/400 instead of 404).
- [ ] **Step 3: Implement.** Registration: replace the two `AddSingleton<IPostingExportWriter, …>` lines with resolve-time factories reading `IOptions<FeaturesOptions>` that skip the disabled writer. Handler: constructor takes `FeatureFlags`; before writer lookup, return the feature-disabled 404 problem when the requested format is off; make the unsupported-format message list only the enabled formats. Controller: `string? format = null`; default resolves `flags.CsvExport ? "csv" : "xlsx"`; both off with no format answers 404.
- [ ] **Step 4: Run.** New tests + full `RunsEndpointTests` + `ExportRunPostingsHandlerTests`. Expected: PASS.
- [ ] **Step 5: Commit.** Message: `feat(exports): gate CSV and XLSX writers on feature flags`.

## Task 6: Docs and full-suite green

**Files:**
- Modify: `docs/02-architecture.md`, `docs/04-solution-and-tooling.md` (Features section lists).

**Interfaces:**
- Consumes: all tasks above.
- Produces: green suites, committed docs.

- [ ] **Step 1: Update the two section lists** with `JobScrape:Features` keys and defaults.
- [ ] **Step 2: Run everything.** `dotnet build JobScrape.slnx` (0 warnings/errors), full `JobScrape.Infrastructure.Tests`, full `JobScrape.Api.Tests`. Expected: all PASS.
- [ ] **Step 3: Commit.** Message: `docs: list Features section in architecture docs`.
