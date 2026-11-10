# Strict backend architecture tests spec

**Goal:** Table-driven NetArchTest guards for backend layering and conventions. The original zero-production-edits stance is lifted; two production fixes landed with the two un-dropped rows.

**Approach:** One new file `StrictArchitectureTests.cs` in `JobScrape.Architecture.Tests`. Static readonly rule tables drive xUnit Theories. NetArchTest covers namespace bans. Raw reflection covers suffix, sealed, and placement. No new packages.

## Rules to ship (each verified green today)

Layer bans:

- Infrastructure avoids Api and Mcp. Infra refs Domain plus Application only. Api row un-dropped: the Serilog tag `ApplicationName` is `static readonly`, not a const, so NetArchTest no longer reads it as a dependency on `JobScrape.Api`.
- Mcp avoids Infrastructure. Mcp refs Application only.
- Mcp avoids EFCore, Playwright, AngleSharp, ClosedXML, and Dapper namespaces.
- Api avoids Microsoft.EntityFrameworkCore, Playwright, AngleSharp, ClosedXML, and Dapper namespaces. Source-level holds today. EFCore row un-dropped: `UsageQuotaMiddleware`'s `DbUpdateException` catch and `ChangeTracker.Clear()` moved into the Infrastructure implementation of `IUsageQuotaRepository.EnsureSeededAsync`, so the middleware carries no EFCore using.

Conventions (selection is identity-first: a type matches by the port it implements or the type it extends, across all production assemblies; a type that never sits where the rule watches cannot pass silently. Namespace placement checks are boundary-exact: `PersistenceLegacy` is not `Persistence`):

- 32 handlers end with Handler and are sealed. Placement under `Application.Features` is boundary-exact.
- 11 controllers end with Controller and are sealed. Placement under `Api.Controllers` is boundary-exact.
- 12 DomainEvent subclasses, selected by base type across production assemblies, are sealed and live under `Domain`. Visibility is not required; sealed is the only shape rule the spec ever set. Internal events are fine.
- Repositories, selected as implementers of any Application `*Repository` interface or `IIdempotencyStore`, live under `Persistence.Repositories` and are internal sealed. Identity-first selection also stops legitimate stores placed in Repositories from tripping this row.
- Read stores, selected as implementers of `IRunStore`, `ISessionStore`, or `ISearchProfileStore` across production assemblies, live under `Persistence` and are public sealed. Public is required.
- `IJobSource` implementers, selected across production assemblies, live under `Infrastructure.Sources` and are sealed.
- `JobScrapeDbContext` lives in `Persistence` and is sealed. Visibility is not required; the invariants are placement and sealed, and the spec only ever said Persistence plus sealed.

## Dropped candidates with why

- Api avoids Mcp. By design, not a violation. Api hosts Mcp (`AddMcpServer`/`MapMcp` in `Program.cs`); the csproj reference is the host design.
- Transitive package-graph bans. Dropped. Namespace-level rows already enforce real usage; `PrivateAssets` surgery is a separate change.
- Handlers or controllers internal. Would fail. All are public by design.
- `FileArtifactStore` under Persistence. Lives in `Browser`. Allowed exception.
- Config visibility uniformity. 9 public plus 1 internal. No rule.
- NetArchTest dependency scans read `const string` values as dependencies. A class whose only content is `public const string Word = "Dapper"` fails the Api-avoids-Dapper row with no Dapper reference. Unfixable in the test layer; production avoids the pattern via `static readonly` (see `LoggingRegistration`).

## Success criteria

- `dotnet test` on the architecture project is green.
- One RED probe is demonstrated then corrected.
- Review loop exits with zero verified findings.
