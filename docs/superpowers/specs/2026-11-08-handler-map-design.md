# Handler narrator's map — design

**Intent:** Give the owner a plain-language map of every handler in the backend so they can explain the codebase's layers and patterns to others with confidence. **Why:** the owner is a plain C# developer who doesn't know the handler concept; XML comments describe pieces but not how pieces connect, so any follow-up question feels risky.

**Goals:** One document that (1) explains the handler pattern in two paragraphs with a real call example, (2) lists every production handler with its one job, who calls it, and what it needs, (3) gives a 3-sentence story for narrating any run through the system.

**Non-goals:** No code moves, no renames, no collapsing partials — behavior and structure stay exactly as they are. Teaching DI/ports beyond what a narrator needs is out.

**Anti-goals:** A second XML-comment layer that repeats each file instead of connecting them; a doc that drifts from the code (every row must name real files and signatures).

**Constraints:** Handler list must match code (32 handlers under `src/backend/JobScrape.Application/Features/`, verified 2026-11-08 against `HandleAsync` signatures and callers). No MediatR exists or is proposed — the doc explains the explicit-call pattern as-is (ADR-0011). Public shapes unchanged.

**Approaches considered:**
- (a) Fold each handler's story into one readable file with fewer constructor arguments — real simplification, but moves code before the owner can narrate it, and risks breaking what they can't yet verify.
- (b) Narrator's map document, no code moves — picked: it is the explanation artifact directly, it makes the current structure legible first, and it marks exactly which folds are safe later. Owner chose (b).

**Decided parts:** Doc lives at `docs/superpowers/specs/2026-11-08-handler-map-design.md` (this file); table grouped by feature folder; "Called by" names the real controller/tool class; "Needs" lists constructor types.

**Left to the builder:** Verify each "Called by" cell against the code before handing over; when a handler is added/renamed, add/rename its row in the same change.

## The pattern in two paragraphs

A handler is an ordinary sealed C# class that does one user-visible job and exposes it as one public method, `HandleAsync`. Whatever it needs — database access, settings, the browser — arrives through its constructor (plain constructor injection; the app's composition root in `JobScrape.Infrastructure/Composition/UseCaseRegistration.cs` wires these up). There is no bus, no message dispatch, no base class: the caller names the handler and awaits the method, e.g. `UsersController` holds a `ListUsersHandler` and runs `await _list.HandleAsync(cancellationToken)`. If you know MediatR's request-handler idea, this is that idea with the middleman deleted on purpose, so "find usages" always shows the whole story.

A narration therefore always has three beats: (1) an entry names a job and calls its handler (`RunsController` → `StartRunHandler`), (2) the handler runs its steps top to bottom using domain rules plus infrastructure behind interfaces, (3) it returns a `Result` the entry maps to HTTP/MCP output. The exception the map used to carry is gone: `RunScrapeHandler` also tells its whole story in one file — there is no spread across sheets. The mutable per-run state (`RunScope`) is owned by that one file, and `partial` remains only for the `[LoggerMessage]` source generator, not for behavior.

## The map

### Scraping (1)

| Handler | One job | Called by | Needs |
|---|---|---|---|
| `RunScrapeHandler` (`Features/Scraping/`, one file) | Executes one search profile end to end: gates, session check, pagination, upsert, outcome | `RunCoordinator` (Infrastructure; the only handler not called from entries) | `ScrapeStores`, `JobSourceRegistry`, `SourceSettingsCatalog`, `ISessionManager`, `IArtifactStore`, `IDelay`, `RetryPolicy`, `IRandomSource`, `TimeProvider`, `ILogger` (10 — the widest) |

`RunScrapeHandler` is the one intentional exception to the ~800-line file guideline (roughly 1,326 lines): a single deep module by plan decision, because `RunScope` ownership has to stay in exactly one place. Do not split it while that single-ownership rule holds — a split would recreate the cross-file state drift the simplification removed.

### Runs (6)

| Handler | One job | Called by | Needs |
|---|---|---|---|
| `StartRunHandler` | Starts a run in the background under compliance consent | `RunsController`, `RunActionsTools` (MCP) | `IRunLauncher`, `IRunStore`, `ComplianceConsent`, `ICallerContext` |
| `CancelRunHandler` | Cancels a run, idempotently | `RunsController`, `RunActionsTools` | `IRunLauncher`, `IScrapeRunRepository`, `IUnitOfWork`, `IRunStore`, `TimeProvider`, `ICallerContext` |
| `GetRunHandler` | Gets one run by id | `RunsController`, `RunsTools` (MCP) | `IRunStore` |
| `ListRunsHandler` | Lists recent runs, newest first | `RunsController`, `RunsTools` | `IRunStore` |
| `ListRunPostingsHandler` | Lists the postings one run saw | `RunsController`, `RunsTools` | `IRunStore` |
| `ExportRunPostingsHandler` | Exports one run's postings to CSV/XLSX | `RunsController` | `IRunStore`, `IEnumerable<IPostingExportWriter>` |

### Identity (13)

| Handler | One job | Called by | Needs |
|---|---|---|---|
| `LoginHandler` | Authenticates by email + password | `AuthController` | `IUserRepository`, `IRoleRepository`, `ITokenService`, `IUnitOfWork`, `LoginPolicy`, `TimeProvider` (+ optional `ILogger`) |
| `SetupPasswordHandler` | Completes first login with a new password | `AuthController` | `IUserRepository`, `IUnitOfWork`, `TimeProvider` |
| `ChangePasswordHandler` | Changes password past first login | `AuthController` | `IUserRepository`, `IUnitOfWork` |
| `CreateUserHandler` | Creates a user with a temporary password | `UsersController` | `IUserRepository`, `IRoleRepository`, `IUnitOfWork`, `TimeProvider` |
| `GetUserHandler` / `ListUsersHandler` | Reads one user / lists users | `UsersController` | `IUserRepository`, `IRoleRepository` (role names) |
| `AssignRoleHandler` / `RevokeRoleHandler` | Assigns / revokes a user's role (last-admin guard on revoke) | `UsersController` | `IUserRepository`, `IRoleRepository`, `IUnitOfWork`, `ICallerContext` |
| `CreateRoleHandler` / `GetRoleHandler` / `ListRolesHandler` | Creates / reads one / lists roles | `RolesController` | `IRoleRepository` (+ `IUnitOfWork` on create) |
| `GrantPermissionHandler` / `RevokePermissionHandler` | Grants / revokes a catalog permission on a role | `RolesController` | `IRoleRepository`, `IUserRepository`, `IUnitOfWork` |

### Sessions (4)

| Handler | One job | Called by | Needs |
|---|---|---|---|
| `CreateSessionHandler` | Creates a browser profile in `New` state | `SessionsController` | `IBrowserProfileRepository`, `IUnitOfWork`, `TimeProvider`, `ICallerContext` |
| `GetSessionHandler` / `ListSessionsHandler` | Looks up one / lists browser profiles | `SessionsController`, `SessionsTools` (MCP) | `ISessionStore` |
| `UpdateSessionHandler` | Renames/retargets a browser profile | `SessionsController` | `IBrowserProfileRepository`, `IBrowserProfileWorkspace`, `IUnitOfWork`, `TimeProvider`, `ILogger`, `ICallerContext` |

### SearchProfiles (4)

| Handler | One job | Called by | Needs |
|---|---|---|---|
| `CreateSearchProfileHandler` / `UpdateSearchProfileHandler` | Creates / replaces a saved search profile | `SearchProfilesController`, `SearchProfilesWriteTools` (MCP) | `ISearchProfileRepository`, `IUnitOfWork`, `SourceSettingsCatalog`, `TimeProvider`, `ICallerContext` |
| `GetSearchProfileHandler` / `ListSearchProfilesHandler` | Reads one / lists saved search profiles | `SearchProfilesController`, `SearchProfilesTools` (MCP) | `ISearchProfileStore` |

### Sources, Quotas, Health (4)

| Handler | One job | Called by | Needs |
|---|---|---|---|
| `ListSourcesHandler` | Snapshots every source: policy, gate, quota, circuit | `SourcesController`, `SourcesTools` (MCP) | `SourceSettingsCatalog`, `ComplianceConsent`, `ISourceQuotaRepository`, `ISourceCircuitBreakerRepository`, `TimeProvider` |
| `ListUsageQuotasHandler` / `UpdateUsageQuotaHandler` | Lists / changes quota definitions | `UsageQuotasController` | `IUsageQuotaRepository` (+ `IUnitOfWork` on update) |
| `GetHealthHandler` | Liveness check | `HealthController`, `HealthTools` (MCP) | `TimeProvider` |

## Narrating any job (the 3-sentence script)

"When you click X, the controller calls one handler's `HandleAsync` — for example starting a run calls `StartRunHandler`, which records the run and hands the real work to `RunScrapeHandler` via the run coordinator. The handler runs its steps — check permission, check the browser session, fetch pages, save after every page — using the database and the browser only through the interfaces in its constructor. It returns a result object, and the controller turns that into the HTTP response you see." Swap the names from the table for any other job; the shape never changes.
