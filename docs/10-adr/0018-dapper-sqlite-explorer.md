# ADR-0018: Dapper for the raw SQLite explorer

- **Status:** Accepted
- **Date:** 2026-11-04
- **Deciders:** Project owner

## Context

The cockpit database page reads and writes arbitrary user tables through
`IDatabaseExplorer`, implemented by `SqliteDatabaseExplorer` with hand-rolled
ADO.NET commands and readers. Dynamic table and column names do not fit
static EF Core `DbSet` mappings, so the explorer bypasses EF Core by design.
The owner directed the explorer onto Dapper. ADR-0005 rejected Dapper for
domain writes and read models, where EF Core already covers the need.

## Decision

Use **Dapper** (`2.1.89`) with **Microsoft.Data.Sqlite** (`10.0.14`) inside
`SqliteDatabaseExplorer` only. The `IDatabaseExplorer` contract is unchanged.
Identifier validation and quoting stay, since table and column names cannot
be parameterized. Every query carries the `CancellationToken` through
Dapper's command definition. EF Core remains the source of truth for domain
persistence per ADR-0005.

## Consequences

### Positive

- Less reader code: no manual command, reader, or ordinal mapping.
- Same behavior and exceptions; existing tests keep passing unchanged.
- Cancellation and validation boundaries stay where they were.

### Negative / trade-offs

- One more package in Infrastructure, pinned centrally.
- Dapper `dynamic` rows need a cast to a string-keyed dictionary.

### Follow-ups

- None. The explorer keeps its contract; callers are unaffected.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| Keep hand-rolled ADO.NET | More code for no gain; Dapper is the documented micro-ORM for this shape. |
| EF Core for the explorer | Dynamic tables do not fit static `DbSet` mappings. |
| Dapper for domain writes | Rejected: EF Core covers writes and read models (ADR-0005 stands). |

## References

- <https://www.learndapper.com/>
- [Solution & Tooling](../04-solution-and-tooling.md)
- [ADR-0005](0005-sqlite-ef-core.md)
