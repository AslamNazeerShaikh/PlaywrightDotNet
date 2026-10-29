# ADR-0005: SQLite + EF Core 10

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** Project owner

## Context

The tool is single-user and local-first. It needs transactions, deduplication, run history, querying, and cross-platform operation with minimal setup. The expected volume is thousands of postings, not millions. A server database would add installation/ops burden. The owner knows EF Core from ASP.NET Core.

## Decision

Use **SQLite** as the source of truth via **EF Core 10.0.14** (`Microsoft.EntityFrameworkCore.Sqlite`). Keep mappings provider-agnostic so PostgreSQL can be added later. Use migrations, WAL, one writer, and a dedicated read-model query path.

## Consequences

### Positive

- Zero-ops, single-file database; easy backup/restore.
- Cross-platform (macOS/Linux/Windows).
- Transactions and unique indexes enforce dedupe/idempotency.
- EF Core migrations and LINQ are familiar.
- Good enough for personal volume.

### Negative / trade-offs

- Single-writer concurrency; no networked multi-user access.
- Some PostgreSQL-specific features (JSONB, full-text) are unavailable.
- SQLite type affinities differ from server DBs; tests must use real SQLite.
- Large artifacts belong on the file system, not in the DB.

### Follow-ups

- Enable WAL, `foreign_keys=ON`, `busy_timeout`.
- Unique index `(source_key, external_job_id)`.
- Back up with `.backup`, not a raw copy of a live WAL DB.
- If multi-user/scale appears, add a PostgreSQL provider behind the same repositories.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| PostgreSQL | Ops overhead; no current need. |
| SQL Server | Licensing/ops; not the simplest cross-platform local choice. |
| LiteDB | Smaller ecosystem/EF tooling; less familiar. |
| JSON/files only | No transactions/querying/dedupe. |
| Dapper | EF Core covers writes and read models; one less layer. |

## References

- <https://learn.microsoft.com/ef/core/>
- [Architecture](../02-architecture.md)
- [Domain Model](../03-domain-model.md)
