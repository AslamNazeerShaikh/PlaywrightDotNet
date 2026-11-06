# PR 6 pre-test report (raw diff, untested code)

PR: https://github.com/AslamNazeerShaikh/PlaywrightDotNet/pull/6
Title: Development. State: open. Head `development` 5ce242c. Base `main`.
Stats: 174 files, +15008, -580, 10 commits. Mergeable: clean.
CI: no check runs on head (pending, total 0). Reviews: none. Comments: none.
Local: branch `development` at head. `dotnet build JobScrape.slnx -c Debug` green, 0 warnings, 0 errors.

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

## Fix list from manual test (user-reported, code-verified)

| # | Fix |
|---|-----|
| 1 | Data plane must send bearer on all calls (`api.ts:431-496`). Until then every data card is dead while logged in. |
| 2 | Seed `Operator` and `Viewer` rows alongside `Admin`, or replace ROL-02 free-text form with closed-name picker. Until then ROL-01 shows one role and new users stay role-less. |
| 3 | Give new users a default role at creation, or force role assignment in the create flow. Role-less users exist today. |
| 4 | Reject same-value rotation in setup-password and change-password with `VALIDATION_FAILED` on `newPassword`. |
| 5 | Forbid self role edits: a logged-in user, admin included, cannot assign or revoke its own roles. Other admins can edit any user except themselves. |
| 6 | Role or permission change on another logged-in user must sign that user out with notice. Suggested text: your admin changed your account, please sign in again. Backend already bumps token version on grant, revoke, and assignment, so the wire supports it. Same rule admin to admin, never self. |
