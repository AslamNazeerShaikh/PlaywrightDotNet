# ADR-0012: Serilog and Polly Runtime Hardening

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** Project owner

## Context

JobLens runs unattended on a local machine, drives a real browser, writes artifacts and a SQLite database, and later calls an optional managed API. When a run fails, the operator needs enough structured evidence to reconstruct what happened without reproducing the site.

The existing design already uses Serilog and explicit retry rules for browser navigation, but it rejected Polly and did not define:

- a file-logging pipeline with retention and correlation;
- a single global exception boundary for CLI/API/background work;
- `IHttpClientFactory` and Polly policies for HTTP clients;
- disposal, cancellation, disk-space, and memory rules for long-running work.

The project owner explicitly requested Serilog file logs with date-based retention and enrichers, global exception handling, Polly-based API resilience and fallbacks, and disciplined async/resource management. The optional API fast path is the only HTTP client that justifies Polly; browser navigation keeps its own stop-on-wall policy.

## Decision

Adopt the runtime baseline in [Runtime Hardening](../15-runtime-hardening.md):

1. **Serilog** as the only logging backend.
   - Compact JSON file sink, daily rolling, 100 MB size roll, 90-day retention, 200-file cap.
   - Async file sink with `blockWhenFull: true`; flush on every exit path.
   - Standard enrichers: log context, machine/environment, process, thread, span, assembly, detailed exceptions, demystified stack traces.
   - Correlation through `Activity`/`TraceId` and `LogContext` scopes; no PII, cookies, auth headers, or response bodies in logs.

2. **Global exception handling.**
   - API: `IExceptionHandler` + `AddProblemDetails` + `UseExceptionHandler`; one Problem Details mapper; `TraceId` returned to the caller.
   - CLI: top-level `try/catch/finally` with documented exit codes; `Log.Fatal` once.
   - Background: `BackgroundService` catches, logs, backpressures, and stops on repeated fatal errors.
   - Last-resort `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException` hooks flush logs.

3. **HTTP resilience with Polly v8.**
   - `IHttpClientFactory` typed clients; no per-call `HttpClient`.
   - `Microsoft.Extensions.Http.Resilience` (Polly v8) pipeline: retry with jitter and `Retry-After`, circuit breaker, per-attempt timeout, overall cap.
   - Safe fallbacks only for read-only/optional data; no fallback for writes, compliance, sessions, or source substitution.
   - No manual retry loop stacked on top of Polly.

4. **Async and resource discipline.**
   - Async end to end, `CancellationToken` on every I/O boundary, no sync-over-async.
   - Bounded channels and `Parallel.ForEachAsync`; no unbounded fire-and-forget.
   - `await using`/`using` for every owned resource; deterministic disposal order.
   - Async file I/O, atomic writes, disk-space checks, artifact/log retention, SQLite WAL/checkpoint rules.
   - No `GC.Collect()`; measure with `dotnet-counters`/`dotnet-trace` before optimizing.

## Consequences

### Positive

- One searchable, correlated log stream per process with enough context to debug a failed run.
- Disk usage is bounded by retention and size caps.
- Transient HTTP failures are handled consistently without retry storms.
- The CLI, API, and background workers share one exception/error-code contract.
- Fewer leaked sockets, file handles, browser processes, and orphaned artifacts.
- Tests can assert structured properties, retries, cancellation, and disposal.

### Negative / trade-offs

- More packages and configuration than `Console.WriteLine` or a single `HttpClient`.
- `blockWhenFull: true` can apply backpressure during a logging flood; this is acceptable for a low-volume scraper but must be monitored.
- Polly policies add a dependency and a learning curve; the rules must stay explicit.
- Logs and artifacts can contain sensitive page data; redaction and retention are mandatory, not optional.
- Circuit breakers and fallbacks can hide upstream problems if they are not logged/metricized; every fallback is a `Warning` event.

### Follow-ups

- Add `IDiskSpace` and a fake for deterministic low-disk tests.
- Add log assertions with `Serilog.Sinks.InMemory`.
- Add Polly tests with a fake `HttpMessageHandler`.
- Add a soak test for handle/memory/WAL stability.
- Revisit Seq/OpenTelemetry only if local JSON logs are insufficient; no external telemetry by default.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| Built-in `Microsoft.Extensions.Logging` console only | No durable, searchable, structured file history or retention. |
| Plain-text Serilog template | Harder to query; compact JSON is still a text file and keeps properties. |
| Serilog synchronous file sink only | File I/O on the request/run thread; async sink keeps latency predictable. |
| Log to SQLite | Couples logging to the database being debugged; log corruption/lock risk. |
| Raw `HttpClient` + manual retry | Duplicated policies, inconsistent backoff, easy to retry unsafe methods. |
| Polly for Playwright navigation | Browser stops are policy decisions, not HTTP transient faults; keep the explicit stop-on-wall rules. |
| Unbounded fire-and-forget background work | Memory growth and lost exceptions. |
| `GC.Collect()`/manual memory trimming | Hurts throughput; masks leaks instead of fixing them. |

## References

- [Runtime Hardening](../15-runtime-hardening.md)
- [Application Patterns](../14-application-patterns.md)
- [Architecture §12](../02-architecture.md#12-cross-cutting-concerns)
- [Operations Runbook](../09-operations-runbook.md)
- [ADR-0009: Test pyramid](0009-test-pyramid-coverage-gates.md)
