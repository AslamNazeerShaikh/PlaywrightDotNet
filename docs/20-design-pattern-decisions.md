# Design Pattern Decisions

> **Purpose:** record the design-pattern review verdicts behind the current shape: which patterns stay, which were rejected and why, and what is deferred.
>
> **Scope:** `JobScrape.Application` ports, `JobScrape.Infrastructure.Browser` seams, error ownership, validation ownership.
>
> **Rule:** a pattern earns its place with a second use case or a second implementation. One implementation gets the plain code.

---

## 1. Kept

### Handler

Each use case is a sealed class with `HandleAsync`, constructor-injected, called directly. No mediator. See [Application Patterns §2](14-application-patterns.md#2-no-cqrs-no-mediatr) and [ADR-0011](10-adr/0011-no-cqrs-mediatr.md).

The scrape handler is split across partials by phase (`RunScrapeHandler.cs`, `RunGate.cs`, `SearchPaginator.cs`, `DetailEnricher.cs`, `WallAndRetry.cs`): one class, one construction site, phases kept in separate files. A split that shared no state would be separate handlers; these share `RunScope`, so partials say so honestly.

### Strategy

`IJobSource` is the Strategy: one adapter per source key (`IndeedJobSource`, `LinkedInJobSource`, `NaukriJobSource`), each classifying walls instead of throwing. `JobSourceRegistry` resolves adapters by `SourceKey`; it builds once from the registered adapters and rejects duplicates. Removing a source removes its registration, never core code.

`SearchPageDriver` (in `Infrastructure.Sources`) holds the pagination mechanics shared by all adapters, so a Strategy stays thin: adapters map pages, the driver walks them.

### Repository

Persistence ports are named repositories with identity lookups (`ISearchProfileRepository`, `IBrowserProfileRepository`, `IRunStore`): `GetAsync`/`FindAsync` by id, no `IQueryable` leaking out, no generic `Find(ISpecification<T>)`. Specifications stay deferred until the second real filter need. See [Application Patterns §5](14-application-patterns.md#5-specification-pattern).

---

## 2. Rejected

### Abstract Factory for adapters

Proposed: a factory creating an `IJobSource` per run. Rejected: adapters hold no per-run state (per-run state lives in `SourceRunContext`, passed as a parameter), so every adapter is a stateless singleton built once at startup. A factory would add a creation ceremony with exactly one creation policy. `JobSourceRegistry` is a lookup table, not a factory, and says so in its name.

`IPlaywrightFactory` keeps the word but is not this pattern: it is a one-method test seam so the session pool runs without launching a browser. Narrow seams keep their plain names.

### Provider

Proposed: custom provider abstractions (a clock provider, a source provider layer). Rejected twice over. The clock is the framework `TimeProvider` (with `FakeTimeProvider` in tests): wrapping it in a project-owned provider renames a BCL seam for no new capability. A "source provider" between the registry and the adapters would forward `Find` with no second implementation behind it. One implementation gets the plain code; the registry already owns lookup.

---

## 3. Deferred: .resx localization

Error codes stay string constants owned by `Errors` (`JobScrape.Application/Results/Errors.cs`). Codes are the UI contract: uppercase snake case, never localized, never repurposed once shipped. Only `Title` and `Detail` are human-readable, and the cockpit renders its own messages from `code` today — there is no localized consumer asking for satellite assemblies.

Verified 2026-11-05: the tree contains zero `ResourceManager` and zero `IStringLocalizer` usages and no `.resx` files. When a second language becomes a requirement, add `.resx` for `Title`/`Detail` only; `Code` stays invariant and `Errors` stays the single factory owner so codes cannot drift between cultures.

---

## 4. Validation has one owner per boundary

Controller clamps are gone. Each boundary validates its own inputs and returns `Errors.ValidationFailed` through `RunIds`:

- Route ids: `RunIds.Required` / `RunIds.InvalidFields` (shared shape: one `VALIDATION_FAILED` carrying every offending field, keyed by JSON field name).
- `StartRunHandler` owns start-request validation; run handlers own theirs.

Handlers never trust the edge, and the edge never second-guesses the handler: invalid input stops at the first owner that sees it.

---

## 5. `ReleaseAsync` takes no `CancellationToken`

`IBrowserSessionPool.ReleaseAsync(BrowserProfileId)` closes a profile's context and flushes cookies to disk. It accepts no token by design: it runs in `RunCoordinator`'s `finally` block, which may execute after the run's token is already cancelled, and a cancelled close would abandon the context and leak the browser. The implementation waits on its gate with `CancellationToken.None`.

**Follow-up:** if a future caller needs a bounded close (a hung browser blocking shutdown), add an overload with an explicit timeout rather than threading the run's token through. The default path must stay uninterruptible.
