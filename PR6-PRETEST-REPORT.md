# PR 6 pre-test report (raw diff, untested code)

PR: https://github.com/AslamNazeerShaikh/PlaywrightDotNet/pull/6
Title: Development. State: open. Head `development` 5ce242c. Base `main`.
Stats: 174 files, +15008, -580, 10 commits. Mergeable: clean.
CI: no check runs on head (pending, total 0). Reviews: none. Comments: none.
Local: branch `development` at head. `dotnet build src/backend/JobScrape.slnx -c Debug` from the repo root green, 0 warnings, 0 errors.

Scope of PR: identity domain plus persistence, JWT edge with policies plus rate limits plus idempotency plus quotas, MCP web-only cutover, admin plus auth cockpit screens, auth endpoint tests. Fixes land only after manual test feedback plus this report combined.

## Must-test-first rollup (CRITICAL plus HIGH)

| # | Area | Finding |
|---|------|---------|
| 1 | Edge | MCP `RequirePermission` skips `must_change_password`. First-login token drives every MCP tool. Test: login as pending user, call `POST /mcp` `list_runs`, expect 403, watch for 200. |
| 2 | Domain | Login returns 403 `PASSWORD_RESET_REQUIRED` for pending vs 401 for unknown. Account oracle. Test: compare pending vs unknown email responses. |
| 3 | Domain | Setup-password returns 409 vs 401 by account state. Same oracle class. Test: unknown email vs pending plus wrong secret. |
| 4 | Domain | Setup path skips lockout check and clears lock state. Locked pending account resets through setup. Test: lock pending account, setup with correct secret, watch for success. |
| 5 | Domain | Last-admin guard is check-then-act with admitted race. Test: two admins revoke concurrently, count admins after. |
| 6 | Edge | `ICallerContext` falls back to `LocalOperator` when unauthenticated. Fail-open default. Test: confirm every route carries auth, probe one without token. |
| 7 | Edge | Login limiter shares 10/min IP partition across login, setup, change, me. Authed `/me` polling self-DoS locks out logins. Test: 11x `GET /me` in 60s, then valid login, watch for 429. |
| 8 | Edge | Idempotency check-then-act has no atomic guard. Parallel duplicate POSTs both execute. Test: two parallel `POST /runs` with same key, count run IDs. |
| 9 | Edge | Quota counters live in `IMemoryCache` per instance. Restart resets. Test: set Api limit 1, call once, restart host, call again, watch for 200 not 429. |
| 10 | Edge | Fresh boot without `ADMIN_EMAIL`/`ADMIN_SETUP_SECRET` starts with zero admins and only logs. Fail-open unusable host. Test: boot fresh DB without env, confirm no path to first admin. |
| 11 | UI | Bearer token in `sessionStorage`, XSS-readable. Test: copy token from DevTools, curl `/api/v1/users` with it. |
| 12 | UI | `handleLogin` drops token when `mustChangePassword`, setup runs unauthenticated, refresh loses context. Test: new-user login, reload on setup screen, confirm bounce. |
| 13 | UI | `isTokenFailure` treats 401 `INVALID_CREDENTIALS` as dead token. Wrong password signs operator out. Test: wrong current password on change-password, watch for sign-out. |
| 14 | UI | `listProfiles/listSources/listSessions/createSession/updateSession` send no bearer. Test: open Profiles with enforcing backend, watch 401 banner with LIVE pill. |
| 15 | Tests | Anonymous matrix asserts `NotEqual(Unauthorized)` only. Empty-body shape unpinned. Test: POST login and setup with `{}` anonymous, record exact status plus code. |
| 16 | Tests | Production placeholder rejection never exercised over HTTP. Test: boot without secret override, confirm startup fails. |
| 17 | Tests | No HTTP test for admin seed, last-admin refusal, quota reset, idempotency TTL and scope, setup oracle timing. Manual test must cover each. |

## Backend domain findings

| File:Line | Sev | Problem | Manual test |
|---|---|---|---|
| `JobScrape.Application/Features/Identity/LoginHandler.cs:85` + `JobScrape.Domain/Identity/User.cs:193` | HIGH | Pending-first-login 403 vs unknown 401 oracle | POST login pending vs unknown, compare codes |
| `JobScrape.Application/Features/Identity/SetupPasswordHandler.cs:65` | HIGH | Unknown/completed 409 vs pending-wrong-secret 401 oracle | POST setup unknown vs pending plus wrong secret |
| `SetupPasswordHandler.cs:75` + `User.cs:150` | HIGH | Setup skips lockout, clears lock state | Lock pending account, setup with correct secret |
| `RevokeRoleHandler.cs:83` | HIGH | Last-admin check-then-act race | Concurrent revoke by two admins, count admins |
| `Infrastructure/Persistence/Configurations/UserConfiguration.cs:34` | HIGH | Roles as JSON GUID array, no FK, dangling ids survive role delete | Delete role row, login as holder, inspect claims |
| `LoginHandler.cs:102` | MEDIUM | Missing roles silently skipped in claims | Same test, token has fewer perm claims, no log |
| `Migrations/20261106151320_FoldProgressIntoRun.cs:13` | HIGH | Up deletes orphan progress rows, Down never restores | Seed orphan row, migrate up then down, row gone |
| `Configurations/ScrapeRunConfiguration.cs:52` | HIGH | Owned Progress mapped non-null over nullable domain, pre-fold runs risk NRE | Load old run without progress row, resume or read |
| `ScrapeRunConfiguration.cs:61` + migration `:22` | MEDIUM | Progress FK cascade deletes history with run | Delete run, inspect progress table |
| `Api/Http/Auth/AuthRegistration.cs:61` | MEDIUM | Bearer key from raw config, bypasses validated options | Short secret boot, compare bearer behavior vs startup failure |
| `Api/Controllers/AuthController.cs:205` | MEDIUM | GET me reads token claims, not DB, stale after grant/revoke | Grant permission, call me with old token |
| `AuthRegistration.cs:98` | LOW | Per-request user lookup on token validation | Authenticated load test, watch user-lookup cost |
| `Domain/Identity/PasswordHash.cs:68` | MEDIUM | PBKDF2 210k sync on request thread | Parallel login burst, p95 plus threadpool queue |
| `Application/Results/Errors.cs:563` | LOW | Lockout detail leaks exact LockedUntilUtc | Trigger lockout, read 403 extension value |
| `Configurations/RoleConfiguration.cs:24` | MEDIUM | permission_keys load without catalog revalidation | Insert unknown perm key, login, decode perm claims |
| `Runtime/AdminSeedHostedService.cs:56` | LOW | Seed empty-check plus multi-step write, no atomic guard | Two instances against empty DB, check duplicates |
| `docs/02-architecture.md:104,574` | LOW | Port counts stale (18 diagram vs 19 text, identity plus quota uncounted) | Count Abstractions plus feature ports vs docs |

## Backend edge findings

| File:line | Sev | Problem | Manual test |
|---|---|---|---|
| `JobScrape.Mcp/Tools/McpAuth.cs:22` | CRITICAL | No `must_change_password` check on MCP tools | Pending token calls list_runs over POST /mcp |
| `Infrastructure/Composition/UseCaseRegistration.cs:47-52` | HIGH | Unauthenticated falls back to LocalOperator | Probe routes without token, confirm 401 everywhere |
| `Api/Controllers/AuthController.cs:159,204` | HIGH | change-password and me rely on fallback only | No-token GET me, confirm 401 depends on fallback |
| `Api/Http/Auth/AuthRegistration.cs:202-216` | HIGH | Login limiter IP-only, shared across login/setup/change/me | 11x GET me then valid login, watch 429 |
| `AuthRegistration.cs:187-201` | MEDIUM | Global limiter IP-keyed, no forwarded headers | 101 authed calls from one host, second client gets 429 |
| `UsersController.cs:19`, `RolesController.cs:19` | MEDIUM | RequireAdmin only, users/roles policies dead | Non-admin users.read token GET users, confirm 403 |
| `Persistence/Repositories/IdempotencyStore.cs:54-83` | HIGH | Check-then-act, no atomic insert | Parallel POST runs same key, count run IDs |
| `Api/Http/IdempotencyMiddleware.cs:68-78` | MEDIUM | Oversize key and anon pass-through silent | 200-char key twice with differing bodies, second executes |
| `IdempotencyMiddleware.cs:81-96` | MEDIUM | Empty-body cancels hash identically, stale replay | Same key across cancels after state change |
| `Api/Http/UsageQuotaMiddleware.cs:224-232,81-92` | HIGH | In-memory counters, restart resets, single node | Limit 1, call, restart, call again |
| `UsageQuotaMiddleware.cs:116-143` | MEDIUM | /quotas consumes Api scope, 404s count | Lower Api to 1, GET quotas twice, second 429 |
| `Api/Program.cs:143-149` | MEDIUM | Quota before idempotency, MCP counted before tool gate | Same-key replay 3x under limit 2, third is 429 |
| `Runtime/AdminSeedHostedService.cs:56-67` | HIGH | Missing seed env only warns, zero-admin host | Fresh DB boot without env, no sign-in path |
| `Runtime/AdminSeedHostedService.cs:80-111` | MEDIUM | Seed creates only Admin role; Operator and Viewer rows never seeded. ROL-01 shows one role, new users are role-less, role picker offers only existing rows | Fresh boot, open Roles as admin, confirm single row; create user, confirm zero roles |
| `SetupPasswordHandler.cs:75-95` + `ChangePasswordHandler.cs:54-62` | MEDIUM | Setup and change-password never reject new secret equal to current secret; same-password rotation accepted | Setup with new equal to temp secret, confirm success; repeat on change-password |
| `Configuration/JwtOptionsValidator.cs:34-46` | LOW | Placeholder plus length check only, weak-long passes | 32x `a` secret boots and verifies |
| `Program.cs:118-128`, health plus auth | LOW | Anonymous matrix holds, MCP get_health comment misleads | Anonymous POST /mcp get_health is 401 |
| `AuthRegistration.GetLoginPartitionKey` | MEDIUM | Limiter partitions login by submitted email, so rotating emails bypasses the 10/min login budget; global IP limiter still applies | Exhaust budget on one email, retry with fresh emails from same IP, watch for no 429 |
| `JobScrape.Mcp/Tools/McpAuth.cs` | HIGH | Pending-token denial has no positive regression test; every MCP fake sets `MustChangePassword => false` | Pending token over POST /mcp, confirm 403 (pending W2a) |
| `Http/ErrorMapper.cs:27-42` | LOW | Mapping correct, regression pin only | Wrong password 401, quota 429 with Retry-After |
| stdio removal | LOW | Host absent, only stale graphify-out refs | ls backend, grep WithStdio outside graphify-out |

## Frontend findings

| File:Line | Sev | Problem | Manual test |
|---|---|---|---|
| `src/client/lib/auth.ts:9` | HIGH | Bearer in sessionStorage, XSS-readable | Copy token, curl /users with it |
| `src/client/lib/auth.ts:66-70` | HIGH | 401 INVALID_CREDENTIALS treated as dead token | Wrong password on change-password signs out |
| `src/client/app.tsx:463-467` | HIGH | handleLogin drops token on mustChangePassword | New-user login, reload setup screen, bounce |
| `src/client/app.tsx:434-446` | HIGH | 403 ACCESS_DENIED keeps stale session | Revoke own read, call me, still signed in |
| `src/client/app.tsx:434-446,769-785` | HIGH | me cached once, role changes never refresh | Grant in second tab, viewer still denied till reload |
| `src/client/app.tsx:545-567` | HIGH | Nav gates only users/roles/quotas | profiles.read-only viewer visits runs/database |
| `src/client/lib/api.ts:431-496` | CRITICAL | Entire data plane sends no bearer: profiles, sources, sessions, runs, postings, exports, all db table calls. Only identity/admin use `authed`. Every protected data card renders UNAUTHENTICATED while logged in: DB-00 zero tables, run history empty, browse profiles, snapshots, PRF-02. Confirmed live: create-profile with full form returns 401 `UNAUTHENTICATED` while signed in as admin. | Log in as admin, open Database, Runs, Sources, Profiles, confirm UNAUTHENTICATED banners |
| `src/client/pages/users.tsx:70-76` | MEDIUM | listRoles fires even after listUsers 403/429 | users.read-only opens Users, two 403s in Network |
| `src/client/lib/api.ts:723-729` | MEDIUM | parseRetryAfter accepts delta-seconds only | Stub 429 with HTTP-date, backoff note missing |
| `src/client/pages/login.tsx:27-33` | MEDIUM | Backoff renders only for two codes with header | 429 without Retry-After shows no guidance |
| `src/client/app.tsx:478-499` | MEDIUM | PASSWORD_SETUP_COMPLETE still sets authProblem | Double setup shows error on sign-in form |
| `src/client/pages/quotas.tsx:70-87` | MEDIUM | window passed raw, no client validation | Clear Window, SAVE, server 400 not inline error |
| `src/client/pages/quotas.tsx:118-195` | MEDIUM | QUO-01 rows misaligned: cell padding, margins, control sizes and shapes out of sync across columns | Table looks unfinished vs Roles and Users tables | Open `#/quotas` beside `#/roles`, compare row heights, input sizes, button shapes |
| `src/client/pages/database-diagram.tsx:94-113` | MEDIUM | Empty-state SVG keeps degenerate viewBox (`0 0 20 56`) under fluid `width:100%;height:auto` CSS, so `No user tables yet` renders full-width and taller than the viewport | Empty database page is dominated by a giant placeholder card | Open `#/database` on empty DB, measure placeholder height vs viewport |
| `src/client/pages/users.tsx:84-92,102-108` | LOW | loadDetail effect omits client dep | Hot-swap BFF, reselect, stale detail |
| `src/client/pages/roles.tsx:60` | LOW | selectedName pins first role, never clears | Create role, panel shows wrong role |
| `src/client/pages/roles.tsx:168-185` | HIGH | ROL-02 free-text New role form against closed RoleName enum (Admin, Operator, Viewer); succeeds at most twice per DB | Dead-end control implies custom roles exist; every later submit errors | Type fourth name, record status plus code |

## Test findings

| File:Line | Sev | Problem | Manual test |
|---|---|---|---|
| `AuthEndpointTests.cs:389-396` | HIGH | Anonymous matrix asserts NotEqual only | POST login/setup `{}` anonymous, record status plus code |
| `ApiFactory.cs:40` + `AuthTestHelper.cs:23` | HIGH | Test-only secret, placeholder rejection untested | Boot without override, confirm startup fails |
| `AuthEndpointTests.cs:305-317` | MEDIUM | NormalizeTrace masks replay divergence | Diff replay bodies byte-for-byte incl traceId |
| `AuthEndpointTests.cs:326-360` | MEDIUM | Rate-limit loop hardcodes 12 plus Retry-After 60 | Spam login 12x, record first-429 attempt |
| `AuthEndpointTests.cs:658-673` | MEDIUM | NeverReplayed depends on jti/timestamp granularity | Same-key double login, compare tokens, decode jti/iat |
| `AuthEndpointTests.cs:107-126,510-518` | MEDIUM | Lockout 5 hardcoded, no unlock/expiry test | 5 fails then correct password 403, retry past 15 min |
| `UsageQuotaEndpointTests.cs:109-131,133-166` | MEDIUM | Window 00:01:00 limit 1, update consumes quota | PUT limit 1 then GET twice, repeat across minute |
| `UsageQuotaEndpointTests.cs:86-106` | MEDIUM | Mcp scope never 429-tested | Lower Mcp to 1, exhaust via MCP path |
| `AuthEndpointTests.cs:520-522` | MEDIUM | Lockout-revokes asserts 401 without code | Reuse old token on /me, record code |
| `IdentityAdminEndpointTests.cs:36-61` | LOW | Weak-password asserts status only | setup with short password, record code |
| `IdentityAdminEndpointTests.cs:120-122` | LOW | Revoked-admin asserts 401 without code | Revoke own Admin via second admin, reuse token |
| `AuthEndpointTests.cs:276-322` | LOW | Replay asserts equality plus count, never 201 | Replay POST users, assert both 201 plus one row |
| `AuthEndpointTests.cs:524-581` | LOW | MustChange token test hand-builds JWT | Real temp-secret login then forged token on profiles |
| `AuthTestHelper.cs:23-26,68-113` | LOW | Test secrets committed, token cached per factory | Grep test-only-secret, revoke then reuse cached token |
| Untested | HIGH | Seed, last-admin refusal, quota reset, idempotency TTL/scope, setup oracle timing | Fresh boot lists seed, DELETE last admin role 409, cross-route key reuse, known-vs-ghost timing |

## Manual test focus checklist

1. Fresh boot with seed env, record seeded admin plus quotas.
2. Login matrix: bad password 401, pending temp secret 403, unknown email 401, compare bodies.
3. Setup matrix: unknown 409 vs pending-wrong-secret 401, double setup 409, weak password code.
4. Lockout: 5 fails, correct password 403, old token dead code, retry after window.
5. Roles: grant then me with old token, revoke own Admin via second admin, last-admin revoke 409.
6. Rate limit: 12 login spams, first-429 attempt plus Retry-After, me-pollution 11x then login.
7. Idempotency: same-key replay identical body, same-key differing body 409, login same-key two tokens differ.
8. Quotas: Api limit 1 double GET, Mcp limit 1 via MCP, restart resets check, /quotas self-429.
9. MCP: anonymous 401, pending-token tool call, no-perm token quota-plus-deny.
10. UI: first-login reload survival, wrong-password no-signout, role-change nav freshness, bearer on profiles/sources/sessions, error plus backoff notes.

### Phase E live verification (2026-11-07, `fix/pr6-pretest-findings` @ de2a8c7)

Backend rebuilt clean (`dotnet build` 0 warnings/0 errors), frontend rebuilt clean (`bun run build`). Fresh DB `/tmp/jobscrape-phasee/jobscrape.db`, seed env `ADMIN_EMAIL=admin@example.com` + `ADMIN_SETUP_SECRET`, JWT secret 64-hex. Backend `https://localhost:5208` (log `/tmp/jobscrape-phasee/backend.log`), cockpit `https://127.0.0.1:3000` (log `/tmp/jobscrape-phasee/cockpit.log`). Chrome 157 headless over CDP for UI flows (shots `/tmp/jobscrape-phasee/shots/`). Servers killed after the run. Result: every Phase E flow PASS, no blocking FAIL. Per-checklist status below; unticked items were not part of Phase E scope and remain unverified.

| # | Result | Evidence |
|---|---|---|
| 1 | Verified | Fresh boot seeded admin + `Admin`/`Operator`/`Viewer` roles (ROL-01 shows 3 rows; NEW ROLE shows "All roles already exist"). Quotas: `Api` 10000/`1.00:00:00`, `Mcp` 1000/`1.00:00:00` (`GET /api/v1/quotas`). |
| 2 | Verified (expectation stale) | Wrong password `401 INVALID_CREDENTIALS`; unknown email `401 INVALID_CREDENTIALS`; pending temp-secret login `401 INVALID_CREDENTIALS` — identical bodies, no account oracle. Item's "pending temp secret 403" no longer matches the wave-0 unified-401 behavior. Bodies compared live, not just codes. |
| 3 | Verified (Wave V) | Setup matrix recorded over HTTP; expectation stale, see Wave V. Unknown email `401 INVALID_CREDENTIALS`; pending plus wrong secret `401 INVALID_CREDENTIALS` (identical body, only traceId differs); double setup `409 PASSWORD_SETUP_COMPLETE`; weak new password `400 VALIDATION_FAILED` (`errors.newPassword`). |
| 4 | Verified (Wave V) | Lockout matrix recorded over HTTP, see Wave V. 5 fails then correct password `403 ACCOUNT_LOCKED`; old token `401 SESSION_REVOKED`; expired lockout seeded in DB, correct login `200`, counters cleared. |
| 5 | Verified (Wave V) | Grant then old token `401 SESSION_REVOKED`; revoke via second admin `200`; admin-A-edits-admin-B `200`; self-revoke and non-admin revoke `403 ACCESS_DENIED`. `LAST_ADMIN_REQUIRED` 409 not HTTP-reachable post self-edit guard; handler-pinned only, see Wave V. |
| 6 | Verified (Wave V) | 11x `GET /me` then login `401` (me does not consume login budget); 12-spam first `429` at attempt 11, `Retry-After: 60`; fresh-email login after exhaustion `429` (IP cap not bypassed by rotating emails). See Wave V. |
| 7 | Verified (Wave V) | Same-key replay `201` with byte-identical body; same key differing body `409 IDEMPOTENCY_KEY_REUSED` (no execution, `wavev-other` absent); same-key login twice yields different tokens (`jti`/`iat` differ). See Wave V. |
| 8 | Verified (Wave V) | Api limit 1: GET `429 QUOTA_EXCEEDED` with `Retry-After`; GET `/quotas` reads exempt (200/200 while exhausted); PUT while exhausted `429`; restart resets counters, definitions persist; Mcp limit 1: first `list_runs` 200, second `429 QUOTA_EXCEEDED`; restart resets, `200` after. See Wave V. |
| 9 | Verified (Wave V, expectation stale) | Anonymous `401 UNAUTHENTICATED`; pending-token tool call denied (`isError:true`, `ACCESS_DENIED`, HTTP 200, not 403); no-perm Viewer token denied for `list_sources`, allowed for `list_runs`. See Wave V. |
| 10 | Partial | Sign-in PASS; first-login reload survival PASS (failed sign-in carries `lastEmail`, setup screen survives reload with email prefilled, setup completes to OVERVIEW); session-expired notice PASS (cookie swapped to expired JWT, reload shows "Your session ended. Sign in again to pick up any account changes." + sign-in form); role-pin stability PASS (Viewer pinned across two refreshes, `aria-current=Viewer`); quota inline validation PASS (`.field-error[data-field=window]` = "Window '' is not a valid time span.", zero mutating requests); empty-DB state PASS-as-rendered (diagram 1134x272px vs 1080px viewport; empty `0 0 360 72` branch unreachable on a migrated DB, pinned only by frontend test `TablesDiagram tables={[]}`); wrong-password change-password `401 INVALID_CREDENTIALS` keeps session cookies PASS (E2E via UI not run; curl only). Bearer on profiles/sources/sessions, role-change nav freshness, error plus backoff notes: NOT run. |

Auth-cookie matrix also verified live: anonymous `GET /api/v1/auth/me` `401 UNAUTHENTICATED`; `POST /bff/session/setup` sets `__Host-jobscrape_session` `Path=/; Secure; HttpOnly; SameSite=Strict` plus `jobscrape_csrf` `Path=/; Secure; SameSite=Strict` (not HttpOnly, by design); `GET /bff/session` returns `{authenticated:false}` anonymous, `{authenticated:true-ish, email, roles, permissions, mustChangePassword:false}` after setup; expired JWT through the BFF proxy answers `401 UNAUTHENTICATED` with session + CSRF cookies cleared (`Max-Age=0`); expired JWT through `/bff/session` answers `{authenticated:false, expired:true}` with cookies cleared; `DELETE /bff/session` answers `204` with all cookies cleared. Sign-out clears cookies client-side only; the stateless JWT stays technically valid until `exp`/`ver` bump, by design.

Two observations, not failures: (a) the AUTH-02 "First login / setup password" button reads stale `lastEmail` (only set by a login submit), so a typed-but-unsubmitted email is dropped and a reload lands back on sign-in; survival works only after a failed sign-in attempt sets `lastEmail`. (b) `GET /bff/session` `mustChangePassword:true` is not issuable on this backend — pending users are refused login (`401 INVALID_CREDENTIALS`) and `setup-password` completes first login in one step — so the `true` branch is only unit-testable, not live-verifiable.

Test caveats: the pending-account fixtures created for this run (`pending@example.com`, password since rotated to `Pending-Pass9!!`) and the `/tmp/jobscrape-phasee/` dir are disposable and can be deleted. `bun run start` (dist/server) exits with `Bundled file "./index-r7ptpdbt.js" not found` under Bun 1.4.4; the BFF was verified via `bun --hot src/server/index.ts` instead.

### Wave V live verification (2026-11-07, `fix/pr6-pretest-findings` @ c263a7d)

Backend on a fresh DB `/tmp/jobscrape-wavev/jobscrape.db` (single squashed migration `20261107112658_InitialCreate` applied; `__EFMigrationsHistory` has that one row), seed env `ADMIN_EMAIL=admin@example.com` + `ADMIN_SETUP_SECRET`, JWT secret 64-hex. Backend `https://localhost:5208` (logs `/tmp/jobscrape-wavev/backend.log` through `backend4.log`), cockpit `https://127.0.0.1:3000` via `bun --hot src/server/dev.ts` (log `/tmp/jobscrape-wavev/cockpit.log`; unused by these items). Backend restarted twice on the same DB for the quota restart-reset checks; the seeded admin survived from the DB each time. Servers killed after the run. No source behavior changes; no commits. Every curl below hit loopback with `-k`; bodies are quoted (bearer tokens elided).

| # | Result | Evidence |
|---|---|---|
| 3 | Verified (expectation stale) | `POST /setup-password`: unknown email `401 INVALID_CREDENTIALS`; pending plus wrong secret `401 INVALID_CREDENTIALS` (bodies identical; traceId rides response headers, which the idempotency store does not replay); weak new password (`"short"`) `400 VALIDATION_FAILED`, `errors.newPassword=["Password must be at least 12 characters."]`; correct setup `200 {"token":...,"mustChangePassword":false}`; double setup `409 PASSWORD_SETUP_COMPLETE`. Checklist "unknown 409" is stale: operator call unified unknown/wrong-secret to 401 while only completed-account setup returns the 409 (`PASSWORD_SETUP_COMPLETE`). Same expectation-stale class as item 2. |
| 4 | Verified | 5x wrong password `401 INVALID_CREDENTIALS`; DB `failed_attempts=5`, `token_version` 1→2, `locked_until_utc` set. Correct password while locked: `403 ACCOUNT_LOCKED` (`Too many failed sign-in attempts; try again later.`). Old token (`ver=1`) on `/me`: `401 SESSION_REVOKED`. Expired lockout seeded in DB (`UPDATE users SET locked_until_utc=0`, `failed_attempts` kept at 5): correct login `200`, counters cleared (`0`, unlocked). No sleep for the lockout window; one 60s wait was for the rate-limit window only. Side observation: the 10/min IP budget shared by login+setup 429'd the 11th call in this sequence (5 setup + 5 fails + 1 locked check all within a minute). |
| 5 | Verified (expectation stale) | Admin A (seeded) assigns Operator to B (`wavev-b@example.com`, Viewer by default): `200`. B's old token on `/me`: `401 SESSION_REVOKED`. B re-login: `roles=["Viewer","Operator"]`. Admin grants `sessions.read` to Viewer: `200`; B's holder token on `/me`: `401 SESSION_REVOKED`; re-login: `sessions.read` in `perm` claims. A self-revoke `DELETE /users/{A}/roles/Admin`: `403 ACCESS_DENIED`. A assigns Admin to B: `200`. A revokes B's Admin (both admins, A-edits-B): `200`; A re-grants Admin to B: `200`. B revokes A's Admin (second admin revokes first): `200`; A's token: `401 SESSION_REVOKED`; A re-login: `roles=[]`; A revokes B: `403 ACCESS_DENIED`. Sole-admin B self-revoke: `403 ACCESS_DENIED`. `LAST_ADMIN_REQUIRED` (409) is not reachable on the deterministic single-admin path: the self-edit guard (`RevokeRoleHandler.cs:83`) returns `403 ACCESS_DENIED` before the last-admin check, and any non-self caller passing the Admin-role caller check keeps `anotherAdmin` true. A stale in-flight admin token can still hit the 409 in a revoke race, and 409 is the correct outcome there. The 409 path is pinned at handler level (`IdentityHandlerTests.cs:799`, `:1132`). Expectation stale post fix-list item 5; not a defect. Admin-A-edits-admin-B `200` confirmed twice. |
| 6 | Verified | 11x `GET /api/v1/auth/me` (valid token): all `200`. Next login attempt: `401 INVALID_CREDENTIALS` — `me` does not consume the login budget. 12x login spam (`spam-wavev@example.com`, wrong password): attempts 1-10 `401 INVALID_CREDENTIALS`; first `429` at attempt 11, `Retry-After: 60`, body `RATE_LIMITED` (`application/problem+json`); attempt 12 also `429`. Fresh-email login after the IP budget was exhausted: `429 RATE_LIMITED` — the login+setup IP-scoped middleware (`LoginIpRateLimitMiddleware.cs`) closes the rotating-email bypass the per-account partition allowed. |
| 7 | Verified | `POST /users` with `Idempotency-Key: wavev-k1-0001`: `201`, `id=227a0c7b-...`. Replay same key+body: `201`, byte-identical body (same id; stored response replayed, headers not replayed). Same key, different body: `409 IDEMPOTENCY_KEY_REUSED`; the differing payload (`wavev-other@example.com`) was never executed (`GET /users` lists only `admin@example.com`, `wavev-b@example.com`, `wavev-replay@example.com`). Same-key login twice: HTTP `200` both, tokens differ (`jti` `c2a46b4b-...` vs `6d613574-...`; `iat` 1794055069 vs 1794055070) — auth paths are exempt from idempotency, tokens never replay. |
| 8 | Verified | `PUT /quotas/Api {limit:1}`: `200`. `GET /users`: `429 QUOTA_EXCEEDED`, `Retry-After: 86035`. `GET /quotas` while exhausted: `200` twice (definition reads are exempt so admins never self-429 on reads). `PUT /quotas/Api` restore while exhausted: `429 QUOTA_EXCEEDED` (writes count; a restore PUT cannot land while the Api budget is spent). Backend restart: `GET /users` `200` (counter reset), immediate second `GET /users` `429` with `Retry-After: 86400`. `PUT /quotas/Api {limit:10000}` after a second restart: `200`; `GET /quotas` shows `Api 10000/Mcp 1000`. `PUT /quotas/Mcp {limit:1}`: `200`. MCP `list_runs` (admin): `200`; immediate repeat: `429 QUOTA_EXCEEDED` (`instance:/mcp`). Restart: `list_runs` `200`; `PUT /quotas/Mcp {limit:1000}`: `200`. Restart resets in-memory counters only; definitions persist in `usage_quotas` (verified after each restart). 404 refunds are pinned by `IdempotencyAndQuotaEdgeTests.cs:213 QuotaRefundsOn404`. |
| 9 | Verified (expectation stale) | Anonymous `POST /mcp` (`list_runs`): `401 UNAUTHENTICATED`. Pending token (synthesized with the run's dev secret; login-issuable pending tokens do not exist on this backend by design, unified-401 refuses pending logins): `list_runs` denied — HTTP `200` with `result.isError:true`, text `ACCESS_DENIED: The account lacks the required role or permission.` Not HTTP 403: tool denials ride the MCP protocol as tool errors (SDK 2.3.1), and `McpAuth` denies on `MustChangePassword` before permission checks. Positive pin exists (`RunsToolsTests.cs:161 PendingCallerIsDenied`). No-perm token (Viewer `wavev-c@example.com`): `list_sources` (needs `sources.read`) denied with the same `ACCESS_DENIED` `isError:true` shape; `list_runs` (Viewer holds `runs.read`) `200`. The requirement (deny pending and no-perm) holds on the wire; the "403" in the checklist is not how the MCP edge reports tool errors. |

Environment observations, not failures: (a) rate-limit budget (10/min) is shared across login+setup per IP, so any sequence burning both (e.g. item 3 then item 4 within a minute) hits `429` on the 11th call — interleaving matters when re-running these matrices. (b) `GET /quotas` reads are exempt from Api consumption; only writes self-429. (c) `/me` sits on the global 100/min IP limiter only. (d) Token `ver` survives a backend restart (DB-backed check); quota counters do not (IMemoryCache) — expected asymmetry, restart is the only counter reset without waiting out the 1-day window. (e) Revoked/locked tokens die within one request of the ver bump; live checks above confirm no stale-token window. (f) Synthetic token minting: for item 9 a pending JWT was minted for `wavev-replay@example.com` (`ver=0`, `must_change_password=true`, correct `sub`/`email`/`role`/`perm`); it validated against the DB and was denied by the MCP gate. The minted token and the throwaway DB holding that account were deleted after the run.

Test pins: every behavior exercised above is pinned at test level, so no test edits were needed. Replay body equality byte-for-byte, headers not replayed (`AuthEndpointTests.cs:290 IdempotencyReplayReturnsIdenticalBodyWithoutDuplicating`), differing-body 409 (`:795`), login never replayed (`:829 LoginResponsesAreNeverReplayed`), expired lockout (`:129 ExpiredLockoutNoLongerBlocksLogin`), locked 403 (`:73`), lockout token revoke (`:529`), login IP cap incl. rotating emails (`:368 LoginIpCapIsNotBypassedByTrailingSlashOrDistinctEmails`), quota reads exempt / mutating consumes / replay does not burn / 404 refunds (`IdempotencyAndQuotaEdgeTests.cs:110,132,169,213`), quota 429 shape plus Retry-After (`UsageQuotaEndpointTests.cs:112`), Mcp scope 429 (`:225 McpScopeExhaustionIs429`), setup oracle (`IdentityAdminEndpointTests.cs:136 SetupOracleDoesNotDistinguishUnknownEmail`), weak password 400 (`:38`), locked-account setup 403 (`:245`), self-assign/self-revoke 403 (`:279,:330`), non-self admin edit then old token revoked (`:377`), old token unauthorized after grant (`:532`), auto-Viewer on create (`:629`), fresh-boot seeds three roles (`:656`), MCP pending deny (`JobScrape.Mcp.Tests/RunsToolsTests.cs:161`), last-admin 409 (`IdentityHandlerTests.cs:799,:1132`).

`/tmp/jobscrape-wavev/jobscrape.db` and every token-bearing file are deleted (verified: no `eyJ...` material remains in that dir); the backend logs (`backend.log`..`backend4.log`, `logs/jobscrape-api-20261107.jsonl`) and non-token artifacts are kept for inspection and contain no tokens or secrets. The fresh DB was never committed; the repo working tree carries this file plus `FIX-PROGRESS.md` from this wave.

## Fix list from manual test (user-reported, code-verified)

| # | Fix |
|---|-----|
| 1 | Data plane must send bearer on all calls (`api.ts:431-496`). Until then every data card is dead while logged in. |
| 2 | Seed `Operator` and `Viewer` rows alongside `Admin`, or replace ROL-02 free-text form with closed-name picker. Until then ROL-01 shows one role and new users stay role-less. |
| 3 | Give new users a default role at creation, or force role assignment in the create flow. Role-less users exist today. |
| 4 | Reject same-value rotation in setup-password and change-password with `VALIDATION_FAILED` on `newPassword`. |
| 5 | Forbid self role edits: a logged-in user, admin included, cannot assign or revoke its own roles. Other admins can edit any user except themselves. |
| 6 | Role or permission change on another logged-in user must sign that user out with notice. Suggested text: your admin changed your account, please sign in again. Backend already bumps token version on grant, revoke, and assignment, so the wire supports it. Same rule admin to admin, never self. |
