# Anonymous mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Anonymous local operation: with `JobScrape:Features:Authentication` false, the API serves every data endpoint without identity, identity endpoints 404, quotas count anonymous callers, and the cockpit skips login.

**Architecture:** Auth services always register; a short-circuit authorization handler approves everything when the flag is off, the caller factory returns a full-grant local operator, one middleware 404s the identity endpoints, and the seed skips only admin creation. The BFF mints CSRF on the anonymous session; the UI boots from the capability endpoint.

**Tech Stack:** .NET 10, ASP.NET Core auth + middleware, React cockpit, Bun tests, xUnit, MTP test runner.

**Spec:** `docs/superpowers/specs/2026-11-08-feature-toggles-design.md`

## Global Constraints

- Flags bind once at host startup and stay immutable per process.
- Fail closed: anonymous mode on a non-loopback bind refuses startup with a logged error. Loopback means hostname `localhost`, `127.0.0.1`, or `::1` only.
- Every default preserves current behavior; the flag ships `true`.
- Application layer takes the plain `FeatureFlags` record, never `IOptions<T>`.
- No new packages.

## Review Focus

- Toggling the flag mid-run is impossible: it is read at startup and per-request from the same immutable options. A test boots both modes separately rather than flipping one host. Pinned in Tasks 2 and 4.
- Anonymous writes without the CSRF cookie fail: the bootstrap must mint it or every anonymous unsafe request 403s. Pinned in Task 8.
- Anonymous and user quotas never share a bucket: keying differs by mode. Pinned in Task 6.
- The login 404 must not break deck boot: `/auth/me` and `/bff/session` stay and report the mode. Pinned in Tasks 4 and 8.
- MCP tools keep working anonymously via the substituted context, not via injected claims. Pinned in Task 3.

---

## File structure

- `src/backend/JobScrape.Infrastructure/Configuration/JwtOptionsValidator.cs` (modify): skip the secret demand when auth is off.
- `src/backend/JobScrape.Api/Http/Auth/AnonymousPassthroughHandler.cs` (create): `IAuthorizationMiddlewareResultHandler` that approves when the flag is off, else delegates to the default handler.
- `src/backend/JobScrape.Api/Http/Auth/AuthRegistration.cs` (modify): register the handler.
- `src/backend/JobScrape.Infrastructure/Composition/UseCaseRegistration.cs` (modify): caller factory returns the full-grant operator when the flag is off.
- `src/backend/JobScrape.Api/Http/AnonymousIdentityEndpointsMiddleware.cs` (create): 404s identity paths when the flag is off.
- `src/backend/JobScrape.Api/Program.cs` (modify): register the middleware before auth; call the loopback guard after build.
- `src/backend/JobScrape.Api/AnonymousBindGuard.cs` (create): static `EnsureAllowed(hosts, anonymous)` throwing on non-loopback.
- `src/backend/JobScrape.Infrastructure/Runtime/AdminSeedHostedService.cs` (modify): optional `IOptions<FeaturesOptions>` parameter; skip admin creation when off.
- `src/backend/JobScrape.Domain/Access/QuotaScope.cs` + `UsageQuotaDefaults.cs` (modify): `Anonymous` member, seed default.
- `src/backend/JobScrape.Api/Http/UsageQuotaMiddleware.cs` (modify): count anonymous callers keyed by IP.
- `src/frontend/src/server/index.ts` (modify): anonymous session bootstrap mints `jobscrape_csrf`.
- `src/frontend/src/client/App.tsx` + pages (modify): features fetch, login skip, nav gating, button hiding.
- Tests: backend `OptionsStartupTests.cs`, new `AnonymousModeTests.cs` (Api.Tests), `UsageQuotaEndpointTests.cs` rows, `AdminSeedHostedServiceTests.cs` rows, `DatabaseEndpointTests.cs`-style new assertions where needed; frontend `app.test.tsx`, `auth-admin.test.tsx`, `runs.test.tsx`, `routes.test.ts` rows.

## Task 1: Validator gating

**Files:**
- Modify: `JwtOptionsValidator.cs`
- Test: `OptionsStartupTests.cs` rows

**Interfaces:**
- Consumes: `FeatureFlags` (Plan 1).
- Produces: placeholder secret boots only when `Authentication` is false.

- [ ] **Step 1: Write the failing test.** `PlaceholderSecretBootsWhenAuthenticationOff`: ApiFactory `UseSetting("JobScrape:Features:Authentication", "false")` plus the placeholder-secret override the existing demo-placeholder test uses; `CreateClient()` must succeed.
- [ ] **Step 2: Run to verify it fails.** `dotnet test JobScrape.Api.Tests --filter "FullyQualifiedName~PlaceholderSecretBootsWhenAuthenticationOff"` from `src/backend`. Expected: FAIL, `OptionsValidationException`.
- [ ] **Step 3: Implement.** Inject `IOptions<FeaturesOptions>` into `JwtOptionsValidator`; return success early for the placeholder/short-secret failures when the flag is false. Keep every other check identical.
- [ ] **Step 4: Run.** Same filter plus the existing placeholder-rejection test. Expected: PASS (rejection test still passes under default flags).
- [ ] **Step 5: Commit.** Message: `feat(auth): bypass JWT secret demand in anonymous mode`.

## Task 2: Authorization short-circuit

**Files:**
- Create: `src/backend/JobScrape.Api/Http/Auth/AnonymousPassthroughHandler.cs`
- Modify: `AuthRegistration.cs`
- Test: new `src/backend/JobScrape.Api.Tests/AnonymousModeTests.cs`

**Interfaces:**
- Consumes: `FeatureFlags`.
- Produces: every `[Authorize]` passes with no principal when the flag is off; default behavior when on.

- [ ] **Step 1: Write the failing tests.** `AnonymousMode_AllowsDataWithoutToken`: flag off, `GET /api/v1/health` and one permission-gated endpoint (e.g. `GET /api/v1/sources`) with no token assert 200. `AuthenticatedMode_StillChallenges`: default flags, same gated endpoint with no token asserts 401.
- [ ] **Step 2: Run to verify they fail.** `--filter "FullyQualifiedName~AnonymousMode_"`. Expected: FAIL (401 on the first).
- [ ] **Step 3: Implement.** Handler class implementing `IAuthorizationMiddlewareResultHandler`: constructor takes `IOptions<FeaturesOptions>` plus the concrete `AuthorizationMiddlewareResultHandler` (the framework registers that implementation; requesting the interface here would resolve back to this decorator, so take the concrete type); when the flag is off, call `context.NoResult()` (succeed without challenge); else delegate to the nested handler.
- [ ] **Step 4: Run.** Same filter. Expected: PASS.
- [ ] **Step 5: Commit.** Message: `feat(auth): anonymous authorization passthrough`.

## Task 3: Full-grant caller substitution

**Files:**
- Modify: `UseCaseRegistration.cs` (caller factory ~lines 48-80)
- Test: `AnonymousModeTests.cs` rows

**Interfaces:**
- Consumes: Task 2 (policies never deny when off).
- Produces: `ICallerContext` is actor `local-operator`, roles `["Admin"]`, every `PermissionCatalog` key when off; unchanged otherwise.

- [ ] **Step 1: Write the failing test.** `AnonymousMode_CallerIsLocalOperator`: flag off, an endpoint echoing the actor (use `GET /api/v1/auth/me`-style echo — if none exists, assert via an MCP tool or quota-actor side effect instead; simplest: assert a usage-quota row keyed by the anonymous actor after one anonymous request).
- [ ] **Step 2: Run to verify it fails.** Expected: FAIL (unauthenticated actor, no quota row).
- [ ] **Step 3: Implement.** In the `AddScoped<ICallerContext>` factory, read `IOptions<FeaturesOptions>`; when off, return `new CallerContext("local-operator", new[] { "Admin" }, <every PermissionCatalog key as string, same enumeration the seed uses>)`. MCP keeps working through this context; do not inject claims.
- [ ] **Step 4: Run.** Same filter. Expected: PASS.
- [ ] **Step 5: Commit.** Message: `feat(auth): substitute local-operator caller in anonymous mode`.

## Task 4: Identity-endpoint 404 middleware

**Files:**
- Create: `src/backend/JobScrape.Api/Http/AnonymousIdentityEndpointsMiddleware.cs`
- Modify: `Program.cs` (register before auth)
- Test: `AnonymousModeTests.cs` rows

**Interfaces:**
- Consumes: `FeatureFlags`.
- Produces: exact-prefix 404s for identity paths when off; everything else passes through.

- [ ] **Step 1: Write the failing tests.** With flag off: `POST /api/v1/auth/login`, `GET /api/v1/users`, `GET /api/v1/roles` assert 404; `GET /api/v1/auth/me` and `GET /api/v1/health` assert 200.
- [ ] **Step 2: Run to verify they fail.** Expected: FAIL (login 400/401, users/roles 401).
- [ ] **Step 3: Implement.** Conventional middleware: exact path prefixes `/api/v1/auth/login`, `/api/v1/auth/setup-password`, `/api/v1/auth/change-password`, `/api/v1/users`, `/api/v1/roles`; when the flag is off, short-circuit with the repo's 404 problem shape (add an `Errors.FeatureDisabled()` catalog member with code `FEATURE_DISABLED` following the neighboring member shape; assert that code in the tests). Register in `Program.cs` before `UseAuthentication`.
- [ ] **Step 4: Run.** Same filter. Expected: PASS.
- [ ] **Step 5: Commit.** Message: `feat(auth): 404 identity endpoints in anonymous mode`.

## Task 5: Seed gating

**Files:**
- Modify: `AdminSeedHostedService.cs`
- Test: `AdminSeedHostedServiceTests.cs` rows

**Interfaces:**
- Consumes: `FeatureFlags`.
- Produces: no admin user created when off; roles and quotas still seeded.

- [ ] **Step 1: Write the failing test.** `AnonymousMode_SeedsQuotasButNoAdmin`: construct the service with features off (new optional `IOptions<FeaturesOptions>` ctor parameter, existing tests keep compiling unchanged) on an empty test database; assert zero users, three roles, quota definitions present.
- [ ] **Step 2: Run to verify it fails.** Expected: FAIL (admin seeded).
- [ ] **Step 3: Implement.** After the empty-users check, return early from admin creation when the flag is off; role ensure and quota seeding run unconditionally.
- [ ] **Step 4: Run.** Same filter + full `AdminSeedHostedServiceTests`. Expected: PASS.
- [ ] **Step 5: Commit.** Message: `feat(auth): skip admin seed in anonymous mode`.

## Task 6: Anonymous quota bucket

**Files:**
- Modify: `QuotaScope.cs` (add `Anonymous = 2`), `UsageQuotaDefaults.cs` (seed default: limit 10000, 24 h, mirroring Api), `UsageQuotaMiddleware.cs` (guard + key), `UsageQuota.cs` doc line ("per authenticated user") if it names the old rule.
- Test: `UsageQuotaEndpointTests.cs` rows or `UsageQuotaTests.cs` rows.

**Interfaces:**
- Consumes: Task 3 (actor is `local-operator` for all anonymous traffic).
- Produces: anonymous requests counted under an IP-keyed bucket; over-limit 429s.

- [ ] **Step 1: Write the failing tests.** Flag off: one anonymous request creates a quota row for the anonymous scope; with a limit-1 anonymous definition inserted via the repository, the second request asserts 429 with a `Retry-After` header.
- [ ] **Step 2: Run to verify they fail.** Expected: FAIL (bypassed, no counting).
- [ ] **Step 3: Implement.** Remove the `IsAuthenticated` bypass only when the flag is off; key = `context.Connection.RemoteIpAddress?.ToString() ?? "local-operator"`, replacing the `ActorId` in `CounterKey`/`CounterFor` for anonymous requests only. Seed the anonymous default next to the Api/Mcp defaults (flows through the existing seed + lazy paths).
- [ ] **Step 4: Run.** Same tests + full quota suites. Expected: PASS.
- [ ] **Step 5: Commit.** Message: `feat(quotas): count anonymous callers in their own bucket`.

## Task 7: Loopback refusal guard

**Files:**
- Create: `src/backend/JobScrape.Api/AnonymousBindGuard.cs` with `public static void EnsureAllowed(string? hosts, bool anonymous)`
- Modify: `Program.cs` (call after `Build`, before serving)
- Test: new unit test class for the guard (no host needed)

**Interfaces:**
- Consumes: `FeatureFlags`.
- Produces: throw on non-loopback hosts when anonymous; pass otherwise.

- [ ] **Step 1: Write the failing tests.** `EnsureAllowed_RejectsWildcardWhenAnonymous` (`"http://0.0.0.0:5208", true` throws), `EnsureAllowed_AcceptsLocalhostWhenAnonymous` (`"http://localhost:5208;https://127.0.0.1:5208"`, passes), `EnsureAllowed_IgnoresHostsWhenAuthenticated` (any hosts, `false`, passes), `EnsureAllowed_DefaultBindPasses` (`null`/empty, `true`, passes — Kestrel default is localhost).
- [ ] **Step 2: Run to verify they fail.** Expected: FAIL (no type).
- [ ] **Step 3: Implement.** Parse `;`-separated hosts, accept empty/`localhost`/`127.0.0.1`/`::1` (bracketed or not); throw `InvalidOperationException` naming the offending host otherwise, only when anonymous. Program passes `builder.Configuration["urls"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS")`.
- [ ] **Step 4: Run.** Same tests. Expected: PASS.
- [ ] **Step 5: Commit.** Message: `feat(auth): refuse anonymous mode on non-loopback binds`.

## Task 8: BFF anonymous bootstrap

**Files:**
- Modify: `src/frontend/src/server/index.ts` (session handler + CSRF mint)
- Test: `src/frontend/src/tests/server/routes.test.ts` rows

**Interfaces:**
- Consumes: capability endpoint (Plan 1, Task 2).
- Produces: anonymous session reports the mode and sets `jobscrape_csrf`.

- [ ] **Step 1: Write the failing tests.** Session read with the backend flags endpoint reporting `authentication: false` asserts the payload carries `anonymous: true` (exact field name follows the file's existing session shape) and the response sets a `jobscrape_csrf=` cookie.
- [ ] **Step 2: Run to verify they fail.** `bun test src/tests/server/routes.test.ts`. Expected: FAIL.
- [ ] **Step 3: Implement.** In the session handler, fetch the backend features per request (boot-frequency only, no cache); when anonymous, mint the CSRF token with the existing `generateCsrfToken` + `buildSetCookie("jobscrape_csrf", …)` path the login flow uses. Leave the authenticated path untouched.
- [ ] **Step 4: Run.** Same file. Expected: PASS.
- [ ] **Step 5: Commit.** Message: `feat(bff): bootstrap CSRF on anonymous sessions`.

## Task 9: UI anonymous mode

**Files:**
- Modify: `src/frontend/src/client/App.tsx`, affected pages (`runs.tsx` export buttons, health page/card, users/roles routes), `src/frontend/src/client/lib/api.ts` (features fetch)
- Test: `src/frontend/src/tests/client/app.test.tsx`, `auth-admin.test.tsx`, `runs.test.tsx` rows

**Interfaces:**
- Consumes: capability endpoint + Task 8.
- Produces: no login window, operator nav minus users/roles, hidden export/health elements per flag, quotas page visible.

- [ ] **Step 1: Write the failing tests.** Features-off boot: LoginPage absent, operator nav present, users/roles links absent; export buttons hidden when their flag is false; health card hidden when health is false. Follow each file's existing query patterns.
- [ ] **Step 2: Run to verify they fail.** `bun test` on the touched files. Expected: FAIL.
- [ ] **Step 3: Implement.** Fetch `/api/v1/features` at boot next to the session read; gate `LoginPage` on `me === null && !anonymous`; extend `routeDenied`/visible-routes to drop users/roles when anonymous; gate export buttons and the health card on their flags. No other visual changes.
- [ ] **Step 4: Run.** `bun run typecheck` clean, touched test files green, `bun fmt` on touched files.
- [ ] **Step 5: Commit.** Message: `feat(cockpit): anonymous mode UI from capability flags`.

## Task 10: Docs and full-suite green

**Files:**
- Modify: spec-adjacent docs if the plan changed them (leave the spec frozen; record deltas here).

**Interfaces:**
- Consumes: all tasks above.
- Produces: green everything.

- [ ] **Step 1: Run everything.** `dotnet build JobScrape.slnx` 0/0, full backend suites, `bun run typecheck`, full `bun test`, `bun fmt --check` on touched files.
- [ ] **Step 2: Commit any fixups** with `fix:` messages, one per issue.
