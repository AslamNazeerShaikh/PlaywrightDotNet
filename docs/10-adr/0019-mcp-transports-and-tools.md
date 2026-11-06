# ADR-0019: MCP server transports and tool surface

- **Status:** Accepted
- **Date:** 2026-11-05
- **Deciders:** Project owner

## Context

Agents (local CLIs and loopback HTTP clients) need typed access to JobScrape
use cases without going through the cockpit. The .NET MCP SDK offers two
loopback transports: stdio for agent CLIs and Streamable HTTP (stateful or
stateless) for HTTP clients. Neither transport carries an identity today, so
every call runs as the local operator. The tool surface must stay small and
auditable: long-running scrapes and raw table access do not belong in it.

## Decision

Ship MCP on **two loopback transports** sharing the API's validated Options,
stores, and handlers:

1. **Stdio first** (`JobScrape.Mcp.Host`): serves agent CLIs. Logs go to
   stderr so stdout stays clean JSON-RPC.
2. **Stateless Streamable HTTP second** (`MapMcp("/mcp")` in `JobScrape.Api`
   with `Stateless = true`): same tools over HTTP, no session state.
3. **Stateful HTTP is deferred**: no resumability or multi-turn need justifies
   session bookkeeping yet.

`JobScrape.Mcp` holds **13 tools in 7 sealed classes**, each calling an
explicit use-case handler directly (no mediator, per ADR-0011): `HealthTools`
(`get_health`), `SourcesTools` (`list_sources`), `SessionsTools`
(`list_sessions`, `get_session`), `SearchProfilesTools` (`list_profiles`,
`get_profile`), `SearchProfilesWriteTools` (`create_profile`,
`update_profile`), `RunsTools` (`list_runs`, `get_run`, `list_run_postings`),
`RunActionsTools` (`start_run`, `cancel_run`).

Callers identify through the **`ICallerContext` seam**
(`JobScrape.Application.Abstractions`): hosts resolve it per scope from
whatever identity the transport supplies. Both loopback hosts register
`CallerContext.LocalOperator` until a transport carries a real identity.

**Excluded from the tool set:** `RunScrapeHandler` and `IDatabaseExplorer`.

## Consequences

### Positive

- One handler implementation serves the API, stdio, and HTTP edges.
- The tool list is explicit (one fixed `.WithTools(McpTools.All)` list shared
  by both hosts); adding a tool is a visible diff, not assembly scanning.
- Write tools reuse handler validation, so agent input fails with the same
  codes as the cockpit.

### Negative / trade-offs

- Two composition roots (`Api/Program.cs`, `Mcp.Host/Program.cs`) register
  the same handlers; they must be kept in mirror.
- Stateless HTTP keeps no progress: long runs poll `get_run` by id.

### Follow-ups

- Stateful HTTP if a resumable multi-turn need arrives (new ADR then).
- Real caller identity when a transport supplies one; handlers already accept
  `ICallerContext` so no signature change is needed.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| Stateful HTTP now | Session bookkeeping with no resumability need; stateless covers poll-by-id. |
| HTTP only, no stdio host | Agent CLIs speak stdio; forcing HTTP adds a hop for the primary consumer. |
| Tool over `RunScrapeHandler` | The scrape holds a scoped unit of work and blocks; `start_run` returns the recorded run at once and the agent polls `get_run`. Direct scrape would stall the JSON-RPC channel and break stateless HTTP. |
| Tool over `IDatabaseExplorer` | Arbitrary user-table read/write bypasses use-case handlers and their validation; agents stay on typed handlers with stable error codes. |
| Mediator-dispatched tools | Rejected: explicit constructor-injected handlers, called directly (ADR-0011 stands). |

## References

- [Application Patterns §9](../14-application-patterns.md) (MCP tool conventions)
- [ADR-0011](0011-no-cqrs-mediatr.md)
- [ADR-0005](0005-sqlite-ef-core.md)
