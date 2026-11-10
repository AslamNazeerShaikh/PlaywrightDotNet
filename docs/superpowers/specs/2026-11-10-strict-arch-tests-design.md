# Strict backend architecture tests spec

**Goal:** Table-driven NetArchTest guards for backend layering and conventions. The original zero-production-edits stance is lifted; two production fixes landed with the two un-dropped rows.

**Approach:** One new file `StrictArchitectureTests.cs` in `JobScrape.Architecture.Tests`. Static readonly rule tables drive xUnit Theories. NetArchTest covers namespace bans. Raw reflection covers suffix, sealed, placement, inheritance, and constructor visibility. No new packages.

## Rules to ship (each verified green today)

Layer bans:

- Infrastructure avoids Api and Mcp. Infra refs Domain plus Application only. Api row un-dropped: the Serilog tag `ApplicationName` is `static readonly`, not a const, so NetArchTest no longer reads it as a dependency on `JobScrape.Api`.
- Mcp avoids Infrastructure. Mcp refs Application only.
- Mcp avoids EFCore, Playwright, AngleSharp, ClosedXML, and Dapper namespaces.
- Api avoids Microsoft.EntityFrameworkCore, Playwright, AngleSharp, ClosedXML, and Dapper namespaces. Source-level holds today. EFCore row un-dropped: `UsageQuotaMiddleware`'s `DbUpdateException` catch and `ChangeTracker.Clear()` moved into the Infrastructure implementation of `IUsageQuotaRepository.EnsureSeededAsync`, so the middleware carries no EFCore using.

Conventions (selection is identity-first where a port or base type exists: a type matches by the port it implements or the type it extends, across all production assemblies; a type that never sits where the rule watches cannot pass silently. The aggregate roots row selects by shape (through `ShapeFirst`) and asserts the base type, so the assertion cannot select its own subjects; the aggregate constructors row selects by base type across all five production assemblies. Namespace placement checks are boundary-exact: `PersistenceLegacy` is not `Persistence`):

- 32 handlers end with Handler and are sealed. Placement under `Application.Features` is boundary-exact.
- 11 controllers end with Controller and are sealed. Placement under `Api.Controllers` is boundary-exact.
- 12 DomainEvent subclasses, selected by base type across production assemblies, are sealed and live under `Domain`. Visibility is not required; sealed is the only shape rule the spec ever set. Internal events are fine.
- Repositories, selected as implementers of any Application `*Repository` interface or `IIdempotencyStore`, live under `Persistence.Repositories` and are internal sealed. Identity-first selection also stops legitimate stores placed in Repositories from tripping this row.
- Read stores, selected as implementers of `IRunStore`, `ISessionStore`, or `ISearchProfileStore` across production assemblies, live under `Persistence` and are public sealed. Public is required.
- `IJobSource` implementers, selected across production assemblies, live under `Infrastructure.Sources` and are sealed.
- `JobScrapeDbContext` lives in `Persistence` and is sealed. Visibility is not required; the invariants are placement and sealed, and the spec only ever said Persistence plus sealed.
- 9 aggregate roots are sealed and inherit `AggregateRoot`. Selection is shape-first through the `ShapeFirst` flag, never by base type and never gated on `Exclude`: concrete, non-record, non-static, non-exception classes in the Domain assembly. The shape yields 13 candidates, so four declared non-aggregate shapes are excluded by exact type: `PasswordHash` (value object implemented as a sealed class and read through an EF value converter), `PolitenessPolicy` and `RetryPolicy` (domain services holding an injected `IRandomSource`), and `ScrapeRunProgress` (owned state inside `ScrapeRun`, mapped as an EF owned type). The 9 checked types are `BrowserProfile`, `SourceCircuitBreaker`, `SourceQuota`, `UsageQuota`, `Role`, `User`, `JobPosting`, `ScrapeRun`, and `SearchProfile`. Static classes drop out through `IsAbstract`, records through the record discriminator (`<Clone>$` or a non-public `EqualityContract`; a public `EqualityContract` property alone no longer qualifies), exceptions through `Exception` assignability, compiler-generated types through the existing `IsCompilerGenerated` guard. The row pins `ExpectedMatches: 9`, so a candidate that silently drops out (e.g. a hand-written class marked `[CompilerGenerated]`) fails the count assertion instead of passing on eight. Exemptions are explicit, so a new non-record class in Domain fails this row until it becomes an aggregate root or is added here with a reason.
- Aggregate roots expose no public instance constructor. Scope is every `AggregateRoot` subclass across all five production assemblies, selected by base type and pinned to `ExpectedMatches: 9`; the first row backstops that base link, so a type that stops inheriting cannot pass silently, and a public unsealed aggregate added to any production assembly fails this row. The rule is "no public instance constructor", not "at least one private constructor": private parameterized constructors and the private parameterless constructors EF Core uses stay legal, and an internal or protected constructor would also pass. All 9 expose only private constructors today.
- The two rows add optional `ConventionScope` fields, defaulted so the existing rows are untouched: `Type? RequiredBaseType` (an assertion, distinct from the `BaseType` selector), `Type[]? Exclude` (declared shape exemptions, purely exemptions now), `bool RequireNonPublicCtors`, `int? ExpectedMatches` (exact-equality assertion on the matched count; rows without it keep the non-empty guard), and `bool ShapeFirst` (gates the shape-first filters: non-record, non-exception. Set on the roots row only). `Violations` gains the two checks beside the sealed check: `RequiredBaseType.IsAssignableFrom(type)` and `type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length == 0`. `Exclude` filters inside `SelectMatching` before the count guard, and `ExpectedMatches: 9` makes an over-broad exemption or a silently narrowed match set fail loudly instead of hollowing a row.

```csharp
[new ConventionScope("Aggregate roots", [Domain], null, null, null, null, Visibility.Any,
    RequiredBaseType: typeof(JobScrape.Domain.Abstractions.AggregateRoot),
    ShapeFirst: true,
    ExpectedMatches: 9,
    Exclude:
    [
        typeof(JobScrape.Domain.Identity.PasswordHash),
        typeof(JobScrape.Domain.Access.PolitenessPolicy),
        typeof(JobScrape.Domain.Access.RetryPolicy),
        typeof(JobScrape.Domain.Sourcing.ScrapeRunProgress),
    ])],
[new ConventionScope("Aggregate constructors", Production, null,
    typeof(JobScrape.Domain.Abstractions.AggregateRoot), null, null, Visibility.Any,
    RequireNonPublicCtors: true,
    ExpectedMatches: 9)],
```

## Dropped candidates with why

- Api avoids Mcp. By design, not a violation. Api hosts Mcp (`AddMcpServer`/`MapMcp` in `Program.cs`); the csproj reference is the host design.
- Transitive package-graph bans. Dropped. Namespace-level rows already enforce real usage; `PrivateAssets` surgery is a separate change.
- Handlers or controllers internal. Would fail. All are public by design.
- `FileArtifactStore` under Persistence. Lives in `Browser`. Allowed exception.
- Config visibility uniformity. 9 public plus 1 internal. No rule.
- NetArchTest dependency scans read `const string` values as dependencies. A class whose only content is `public const string Word = "Dapper"` fails the Api-avoids-Dapper row with no Dapper reference. Unfixable in the test layer; production avoids the pattern via `static readonly` (see `LoggingRegistration`).

## Success criteria

- `dotnet test` on the architecture project is green.
- Both aggregate rows report exactly the 9 types named above; the shape row's exemption list holds exactly the 4 named types. A permanent `ExpectedMatches: 9` assertion confirms both match sets on every run, not just at review time.
- RED probes cover the two new check kinds: point `RequiredBaseType` at `DomainEvent` and watch the aggregate row fail on all 9 aggregates; make one aggregate constructor public and watch the constructor row fail on that type. Both probes are reverted before the final run.
- One RED probe is demonstrated then corrected.
- Review loop exits with zero verified findings.
