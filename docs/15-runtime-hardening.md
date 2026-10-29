# Runtime Hardening: Logging, Errors, Resilience, and Resources

> **Purpose:** define the production baseline for structured logging, global exception handling, HTTP resilience, async/cancellation, disposal, and CPU/RAM/disk/network hygiene.
>
> **Applies to:** the v1 CLI and the v2 .NET API. The React/Bun cockpit consumes the API contract; it does not implement these rules.
>
> **Rule:** every run must be diagnosable from its logs and artifacts alone; every failure must have a stable code; no queue, file, buffer, or memory pool may grow without a bound.

---

## 1. Baseline decisions

| Concern | Decision |
|---|---|
| Logging | Serilog with compact JSON file sink, daily rolling, 90-day retention, standard enrichers, async writes, redaction |
| Global errors | ASP.NET Core `IExceptionHandler` + Problem Details for the API; explicit exit-code mapping for the CLI; last-resort process hooks |
| HTTP clients | `IHttpClientFactory` typed clients + `Microsoft.Extensions.Http.Resilience` (Polly v8) retry, circuit breaker, timeout, fallback |
| Async | `async`/`await` end to end, `CancellationToken` on every I/O boundary, no sync-over-async, no unbounded fire-and-forget |
| Resources | `await using`/`using`, bounded channels, streaming, atomic file writes, disk-space checks, retention cleanup |
| Verification | Structured-log assertions, fake HTTP handlers, cancellation/disposal tests, soak runs, `dotnet-counters`/`dotnet-trace` |

Related decisions: [Application Patterns](14-application-patterns.md), [AOT-Friendly Coding](16-aot-and-trimming.md), [ADR-0012](10-adr/0012-serilog-polly-runtime-hardening.md), [ADR-0013](10-adr/0013-aot-friendly-policy.md), [Anti-Bot & Compliance](07-anti-bot-and-compliance.md).

---

## 2. Structured logging with Serilog

### 2.1 Layering

- Domain and Application use `Microsoft.Extensions.Logging.ILogger<T>` only. They do not reference Serilog.
- Infrastructure may use `ILogger<T>`; it does not configure sinks.
- `JobLens.Cli` and `JobLens.Api` own Serilog configuration, enrichers, sinks, and shutdown.
- No `Console.WriteLine` for diagnostics; no interpolated log messages.

### 2.2 Pipeline

`Serilog.AspNetCore` already brings the console, file, compact formatter, settings, and hosting integration. Add only the extra pieces:

| Package | Version | Purpose |
|---|---|---|
| `Serilog.Sinks.Async` | `2.1.0` | Keep file I/O off the request/run thread |
| `Serilog.Enrichers.Environment` | `3.0.1` | Machine and environment names |
| `Serilog.Enrichers.Process` | `3.0.0` | Process ID and name |
| `Serilog.Enrichers.Thread` | `4.0.0` | Thread ID |
| `Serilog.Enrichers.Span` | `3.1.0` | `TraceId` / `SpanId` from `Activity.Current` |

`Serilog.AspNetCore` is already in the package matrix. `Serilog.Formatting.Compact`, `Serilog.Settings.Configuration`, `Serilog.Sinks.Console`, and `Serilog.Sinks.File` come with it. The settings package is present transitively but must not be used.

Serilog is a **vendor exception** under the AOT policy. Do not call `ReadFrom.Configuration` (reflection-based package binding), do not destructure objects, and do not add reflection enrichers. Configure explicitly in code and log scalar properties only.

### 2.3 Bootstrap

`LoggingOptions` is a small bound record:

```csharp
public sealed class LoggingOptions
{
    public LogEventLevel MinimumLevel { get; init; } = LogEventLevel.Information;
    public int RetentionDays { get; init; } = 90;
    public long FileSizeLimitBytes { get; init; } = 100 * 1024 * 1024;
}
```

```csharp
// Source-generated configuration binding; see the AOT policy.
var logOptions = builder.Configuration
    .GetSection("JobLens:Logging")
    .Get<LoggingOptions>() ?? new LoggingOptions();

var applicationName = role == "api" ? "JobLens.Api" : "JobLens.Cli";

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(logOptions.MinimumLevel)
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProcessId()
    .Enrich.WithProcessName()
    .Enrich.WithThreadId()
    .Enrich.WithSpan()
    .Enrich.WithProperty("Application", applicationName)
    .Enrich.WithProperty("Version", BuildInfo.Version)
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .WriteTo.Async(
        configure: sink => sink.File(
            path: Path.Combine(logRoot, $"joblens-{role}-.jsonl"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 200,
            retainedFileTimeLimit: TimeSpan.FromDays(logOptions.RetentionDays),
            fileSizeLimitBytes: logOptions.FileSizeLimitBytes,
            rollOnFileSizeLimit: true,
            formatter: new CompactJsonFormatter()),
        bufferSize: 10_000,
        blockWhenFull: true)
    .CreateLogger();

// Attach the logger to the host:
builder.Host.UseSerilog();          // IHostBuilder
// builder.Services.AddSerilog();   // HostApplicationBuilder
```

- `role` is `cli` or `api`; separate prefixes avoid two processes fighting over one file.
- `blockWhenFull: true` makes logging lossless under pressure; the alternative is dropped events. For this low-volume app, backpressure is the safer default. Monitor buffer pressure if it ever changes.
- `retainedFileCountLimit` and `retainedFileTimeLimit` are applied together: a file is kept while it is within both limits.
- `rollOnFileSizeLimit` prevents one 100 MB file per day from becoming unbounded.
- `Log.CloseAndFlush()` must run on every exit path.

`LoggingOptions` is bound with the configuration source generator and validated at the composition root ([Solution & Tooling §5.3](04-solution-and-tooling.md#53-options-binding)). `appsettings.json` controls levels and retention; thresholds are not hard-coded:

```json
{
  "JobLens": {
    "Logging": {
      "MinimumLevel": "Information",
      "RetentionDays": 90,
      "FileSizeLimitBytes": 104857600
    },
    "Storage": {
      "MinimumFreeDiskBytes": 1073741824
    },
    "Browser": {
      "MaxHtmlBytes": 10485760,
      "MaxScreenshotBytes": 5242880,
      "MaxTraceBytes": 104857600
    }
  }
}
```

### 2.4 Enrichers

Always on:

| Enricher | Property | Why |
|---|---|---|
| `FromLogContext` | scoped properties | `RunId`, `Source`, `JobId`, `CorrelationId` |
| `WithMachineName` | `MachineName` | Multi-machine support bundles |
| `WithEnvironmentName` | `EnvironmentName` | Development vs production |
| `WithProcessId` / `WithProcessName` | `ProcessId`, `ProcessName` | CLI vs API vs test host |
| `WithThreadId` | `ThreadId` | Diagnose thread-pool starvation |
| `WithSpan` | `TraceId`, `SpanId` | Correlate API, HTTP, and run events |
| `WithProperty` | `Application`, `Version` | App identity from a compile-time `BuildInfo` constant |

Application-scoped (not global mutable state):

| Property | Source |
|---|---|
| `Application` | `"JobLens.Cli"` or `"JobLens.Api"` |
| `Version` | Generated `BuildInfo.Version` from `$(Version)` |
| `RunId` | Run scope |
| `Source` | Source scope |
| `JobId` | Per-posting scope |
| `CorrelationId` | CLI startup or API middleware |
| `RequestId` / `RequestPath` / `StatusCode` / `Elapsed` | API request logging |

Optional, not default:

- `WithMemoryUsage` — useful during a memory investigation, too noisy and too costly per event for normal runs.
- `WithCorrelationId` — redundant when `WithSpan` is enabled.

Do not use reflection-based enrichers (`Serilog.Exceptions`, `Serilog.Enrichers.AssemblyName`, `Serilog.Enrichers.Demystifier`, sensitive-data/client-info enrichers) under the AOT policy. Use explicit scalar properties and explicit redaction.

Never enrich with environment user names, full request/response bodies, cookies, authorization headers, or raw HTML.

### 2.5 Correlation and scopes

API middleware creates one correlation ID per request and pushes it into `LogContext`; `WithSpan` adds `TraceId`/`SpanId` from `Activity.Current`. The same `TraceId` is returned in Problem Details.

```csharp
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? Activity.Current?.TraceId.ToString()
        ?? Guid.NewGuid().ToString("N");

    context.Response.Headers["X-Correlation-Id"] = correlationId;
    using (LogContext.PushProperty("CorrelationId", correlationId))
    using (LogContext.PushProperty("RequestId", context.TraceIdentifier))
    {
        await next();
    }
});
```

CLI creates a correlation ID at startup and uses it for the whole invocation.

Run and source scopes:

```csharp
using (LogContext.PushProperty("RunId", run.Id.Value))
using (LogContext.PushProperty("Source", run.Source.Value))
{
    // page/detail work
}
```

Never put a `RunId` in a static global property; concurrent runs must not share it.

### 2.6 Message templates and performance

- Use named placeholders: `_logger.LogInformation("Fetched {JobCount} jobs from {Source} in {ElapsedMs} ms", count, source, elapsed);`
- Never interpolate: `_logger.LogInformation($"Fetched {count}")` loses structure and allocates.
- Use `[LoggerMessage]` source-generated methods on hot paths; the `CA1848` analyzer enforces this.
- Guard expensive argument construction with `if (_logger.IsEnabled(LogLevel.Debug))`.
- Log scalar properties only. Never use `{@...}` object destructuring; Serilog's destructuring path is reflection-based and violates the AOT policy.
- Log counts, IDs, hashes, sizes, and paths — not page bodies.
- Do not log the same exception at every layer. Log once at the boundary that decides the outcome.

### 2.7 Redaction

- Cookies, `Authorization`, `Set-Cookie`, API keys, and form values are never logged.
- Redact at the call site and in the HTTP handler; do not rely on reflection-based masking enrichers.
- Problem Details `detail` is also safe: no stack traces, SQL, HTML, or filesystem paths.
- Log artifact **paths** and **hashes**, not artifact contents.

### 2.8 Files, rotation, retention, disk budget

- Path: `artifacts/logs/joblens-{role}-YYYYMMDD.jsonl`.
- Rotation: daily and at 100 MB; keep at most 200 files and 90 days.
- Format: `CompactJsonFormatter` (one JSON object per line) for grep/`jq`; console can stay compact JSON for consistency.
- `artifacts/` is gitignored and owner-only.
- Check free disk before a run or export; fail with `STORAGE_LOW_DISK` when below `JobLens:Storage:MinimumFreeDiskBytes`.
- Do not delete open/active log files during cleanup; let the sink own its retention.
- On startup, clean only expired **temp** files and orphaned artifacts, never the current run's directory.

### 2.9 Shutdown

```csharp
try
{
    await app.RunAsync();
}
finally
{
    Log.CloseAndFlush();
}
```

The async sink must be flushed, or buffered events are lost. Treat a missing final log line as a shutdown bug.

---

## 3. Global exception handling and error logging

### 3.1 Rules

- Expected failures return `Result<T>` with a stable `Error.Code`; they are not exceptions.
- Invariant violations throw `DomainException` and are treated as bugs (usually 500).
- `OperationCanceledException` is not an error; do not log it as one and do not convert it to a failure.
- Unexpected exceptions are logged once at the boundary with the full exception, correlation ID, and safe context.
- The API returns Problem Details; the CLI returns an exit code and a stderr message.
- Never swallow an exception to keep a run alive. Stop, record, and let the caller decide.

### 3.2 API

```csharp
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
```

`GlobalExceptionHandler` implements `IExceptionHandler`:

```csharp
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException)
            return false; // client disconnect or shutdown; do not log as a failure

        logger.LogError(
            exception,
            "Unhandled exception for {Method} {Path}. TraceId={TraceId}",
            context.Request.Method,
            context.Request.Path,
            Activity.Current?.Id ?? context.TraceIdentifier);

        await Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal server error",
            detail: "An unexpected error occurred. Use the trace ID when reporting it.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "INTERNAL_ERROR",
                ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier
            }).ExecuteAsync(context);

        return true;
    }
}
```

- Known domain/HTTP outcomes should be handled before this point and mapped through the Result-to-Problem mapper.
- Do not include `exception.Message` in the public `detail`.
- `UseSerilogRequestLogging` emits one completion event per request; do not add a second request log.
- The BFF forwards the problem body and `Content-Type` unchanged.

### 3.3 CLI

```csharp
try
{
    await app.RunAsync();
    return 0;
}
catch (OperationCanceledException)
{
    Log.Information("Run cancelled by the operator");
    return 130;
}
catch (DomainException ex)
{
    Log.Error(ex, "Domain invariant failed");
    return 1; // invariant violation is a bug, not a configuration error
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled CLI failure");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
```

Exit codes remain the contract in [Solution & Tooling §6.1](04-solution-and-tooling.md#61-exit-codes). `STORAGE_LOW_DISK` and `IO_FAILURE` map to `7`; a fatal exception must produce one `Fatal` event with the exception and correlation ID, not a stack trace on stdout.

### 3.4 Background work

- `BackgroundService.ExecuteAsync` catches only to log and decide whether to stop.
- `OperationCanceledException` when `stoppingToken.IsCancellationRequested` is normal shutdown.
- A repeated fatal loop logs, backs off, and calls `IHostApplicationLifetime.StopApplication()` rather than spinning.
- Use a bounded `Channel<T>` for queued work; never `Task.Run` an unbounded loop.

### 3.5 Last-resort hooks

```csharp
AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
{
    Log.Fatal(eventArgs.ExceptionObject as Exception, "Unhandled AppDomain exception");
    Log.CloseAndFlush();
};

TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
{
    Log.Error(eventArgs.Exception, "Unobserved task exception");
    eventArgs.SetObserved();
};
```

These are safety nets, not the primary handler. They cannot save a corrupt process; they preserve the evidence.

### 3.6 What to log for debugging

| Event | Level | Properties |
|---|---|---|
| Run started/completed | Information | `RunId`, `Source`, `ProfileId`, `Outcome`, `ElapsedMs` |
| Page fetched | Information/Debug | `RunId`, `Source`, `Page`, `JobCount`, `ElapsedMs` |
| Challenge detected | Warning | `RunId`, `Source`, `ChallengeKind`, `Confidence`, `ArtifactPath` |
| Quota/cooldown/circuit | Warning | `Source`, `Limit`, `Used`, `RetryAfter` |
| HTTP retry/fallback | Warning/Debug | `Attempt`, `StatusCode`, `DelayMs`, `FallbackUsed` |
| Validation failure | Information | `Field`, `Rule`, `RequestId` |
| Unexpected exception | Error/Fatal | `Exception`, `CorrelationId`, `TraceId`, `RunId`, `Source` |
| Low disk | Warning/Error | `Path`, `FreeBytes`, `RequiredBytes` |

Do not log expected 404s, validation failures, or client cancellations at `Error`.

---

## 4. HTTP clients and Polly resilience

### 4.1 Client construction

- Use `IHttpClientFactory`; never `new HttpClient()` per call.
- Prefer a typed client (`ManagedApiClient`) that returns `Result<T>`.
- Do not dispose the injected `HttpClient`; the factory owns the handler lifetime.
- Set one overall `HttpClient.Timeout` and a shorter Polly per-attempt timeout; avoid ambiguous double timeouts.
- Use `SocketsHttpHandler` pooling and connection limits for the optional API fast path.

```csharp
services
    .AddHttpClient<IManagedApiClient, ManagedApiClient>(client =>
    {
        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(60); // overall cap
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    })
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        MaxConnectionsPerServer = 8,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        AutomaticDecompression = DecompressionMethods.All,
        EnableMultipleHttp2Connections = true
    })
    .AddResilienceHandler("managed-api", builder =>
    {
        var retry = new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 2,
            Delay = TimeSpan.FromSeconds(2),
            MaxDelay = TimeSpan.FromSeconds(20),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldRetryAfterHeader = true,
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>()
                .HandleResult(response =>
                    response.StatusCode == HttpStatusCode.RequestTimeout ||
                    response.StatusCode == HttpStatusCode.TooManyRequests ||
                    (int)response.StatusCode >= 500)
        };
        retry.DisableForUnsafeHttpMethods();

        builder
            .AddRetry(retry)
            .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 8,
                BreakDuration = TimeSpan.FromSeconds(30)
            })
            .AddTimeout(TimeSpan.FromSeconds(15));
    });
```

- `MaxRetryAttempts = 2` means at most three attempts. Retries apply to idempotent methods by default; `DisableForUnsafeHttpMethods()` protects POST/PUT/PATCH/DELETE.
- A POST may be retried only when the operation has an idempotency key and the server contract explicitly allows replay.
- Do not stack a manual retry loop on top of the Polly pipeline. One owner per retry decision.
- Browser navigation keeps its explicit retry policy in [Architecture §12.2](02-architecture.md#122-retry-policy); Polly is for HTTP API clients, not Playwright page actions.
- Circuit breakers are per API host/endpoint, not global. A broken managed API must not stop Naukri scraping.

### 4.2 Retry policy

| Condition | Retry? | Notes |
|---|---|---|
| `408 Request Timeout` | Yes | Honor `Retry-After` |
| `429 Too Many Requests` | Yes | Honor `Retry-After`; reduce rate |
| `500`, `502`, `503`, `504` | Yes | Bounded; jitter |
| `HttpRequestException`, connect timeout | Yes | DNS/socket failures |
| `400`, `401`, `403`, `404`, `409`, `422` | **No** | Client/contract/auth failure |
| CAPTCHA/challenge/auth wall | **No** | Never retry; stop and hand off |
| Cancellation | **No** | Propagate `OperationCanceledException` |

### 4.3 Timeouts

- Per attempt: 15 seconds for API calls.
- Overall: 60 seconds including retries and backoff.
- Use `CancellationToken` linked to the request/run and to shutdown.
- A timeout is a `Result` failure (`TIMEOUT` or `API_UNAVAILABLE`), not an unhandled exception.
- Do not set `HttpClient.Timeout` to infinite and rely on a single Polly timeout with no overall cap.

### 4.4 Fallbacks

A fallback is allowed only when the caller can tolerate stale or missing data:

| Call | Fallback |
|---|---|
| Read-only reference data | Last cached value, marked `Stale=true` |
| Optional enrichment | Return the unenriched result; record `EnrichmentSkipped` |
| Health/diagnostics | Return `Unavailable` with the reason |
| Write/start/cancel/export | **No fallback**; return failure |
| Compliance/session decision | **No fallback**; fail closed |
| Scrape a different source | **No fallback**; never substitute |

Rules:

- Log every fallback at `Warning` once per operation, with `FallbackReason` and `Stale`.
- Never hide a compliance block, challenge, or auth wall behind a fallback.
- Never return fabricated data.
- A fallback still returns `Result<T>`; it does not throw.

### 4.5 Response and stream disposal

```csharp
using var request = new HttpRequestMessage(HttpMethod.Get, uri);
using var response = await client.SendAsync(
    request,
    HttpCompletionOption.ResponseHeadersRead,
    cancellationToken);

if (!response.IsSuccessStatusCode)
    return Result<T>.Fail(Errors.ApiUnavailable(response.StatusCode));

await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
```

- Dispose `HttpResponseMessage` and the stream; the factory does not dispose them for you.
- Use `ResponseHeadersRead` for large or streamed bodies; do not buffer a whole export or HTML page in memory.
- Do not read/log an error body that may contain PII or HTML; log status, reason, and correlation ID.
- One `HttpClient` per typed client; no per-call client creation, no socket exhaustion.

---

## 5. Async and cancellation

### 5.1 Rules

- Async all the way; no `.Result`, `.Wait()`, `GetAwaiter().GetResult()`, or `Task.Run` to escape async.
- `CancellationToken` is the last parameter on every async I/O method.
- Do not pass `CancellationToken.None` from a request path. Entry points may create a token; callers must pass it.
- `ConfigureAwait(false)` in Domain/Application/Infrastructure libraries; do not use it in CLI/API top-level code.
- `async void` is forbidden except framework event handlers.
- Do not catch `OperationCanceledException` and continue; mark the run `Cancelled` and keep partial results.
- `ValueTask` only when measured or when returning a cached/synchronous completion; default to `Task`.
- Do not use `Task.WhenAll` over unbounded input; bound concurrency first.

### 5.2 Timeouts and linked tokens

```csharp
using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
timeout.CancelAfter(TimeSpan.FromSeconds(30));

try
{
    return await _client.SendAsync(request, timeout.Token);
}
catch (OperationCanceledException) when (
    !cancellationToken.IsCancellationRequested &&
    timeout.IsCancellationRequested)
{
    return Result<T>.Fail(Errors.Timeout("managed-api"));
}
```

The filter distinguishes an operator/shutdown cancel from a timeout. A user cancel is never converted to a failure.

### 5.3 Bounded concurrency

```csharp
await Parallel.ForEachAsync(
    jobs,
    new ParallelOptions
    {
        MaxDegreeOfParallelism = 4,
        CancellationToken = cancellationToken
    },
    async (job, token) => await EnrichAsync(job, token));
```

- Browser runs remain single-writer (`MaxConcurrentRuns = 1`).
- Detail enrichment may use bounded parallelism, but respect source caps and backoff.
- Prefer `Channel.CreateBounded` over an unbounded `ConcurrentQueue`.
- Use `SemaphoreSlim.WaitAsync(ct)`, never `Wait()`.

### 5.4 Fire-and-forget

Do not start a task without an owner. If work must run in the background:

1. Put it on a bounded channel.
2. Consume it from a `BackgroundService`.
3. Catch and log exceptions in the consumer.
4. Cancel and drain on shutdown.
5. Expose queue depth and failures as metrics.

---

## 6. Resource management

### 6.1 Disposal matrix

| Resource | Pattern | Leak symptom |
|---|---|---|
| `HttpClient` | Injected; do not dispose | Socket exhaustion if created per call |
| `HttpResponseMessage` / stream | `using` / `await using` | Connection pool starvation |
| `FileStream`, readers/writers | `await using` | Locked files, handles |
| `DbContext` / `DbConnection` | `await using` per unit of work | Stale tracking, pool exhaustion |
| `CancellationTokenSource` | `using` | Timer/registration leaks |
| `SemaphoreSlim` | `using` | Handle leaks |
| `Channel<T>` | `Complete()` + drain | Hanging consumers |
| Playwright browser/context/page | `await using`, reverse order | Orphan Chromium processes |
| Playwright trace | `StopAsync` + dispose | Incomplete trace archives |
| `ArrayPool<byte>` buffer | `Return` in `finally` | Pool growth / memory pressure |
| `Activity` / `ActivitySource` | `Dispose` | Listener leaks |
| `Timer` / `PeriodicTimer` | `Dispose` | Background callbacks after shutdown |
| Serilog logger | `CloseAndFlush` | Lost buffered events |

- `await using` declarations dispose in reverse order; use them for browser → context → page.
- Dispose in `finally` when a resource is acquired conditionally.
- Custom session/artifact types implement `IAsyncDisposable` when they own async resources.
- `Dispose` must be idempotent; never throw from `Dispose`.
- Do not dispose objects you did not create (injected `HttpClient`, DI-owned `DbContext`).

### 6.2 I/O and filesystem

- Use async file APIs with `FileOptions.Asynchronous | FileOptions.SequentialScan`.
- Use a 64 KB buffer for large copies; stream rather than `ReadAllBytes` for artifacts/exports.
- Write atomically: temp file in the same directory → flush → `File.Move(temp, final, overwrite: true)`.
- Delete temp files in `finally`; sweep expired temp files at startup.
- Validate paths: `Path.GetFullPath` and ensure the result stays under the configured root; reject traversal.
- Set owner-only permissions for profiles/artifacts on Unix (`0700`).
- Treat `IOException` (sharing violation/lock) as an operational failure with a clear code; do not retry forever.
- Cap HTML (`Browser:MaxHtmlBytes`, 10 MB), screenshots (`MaxScreenshotBytes`, 5 MB), and traces (`MaxTraceBytes`, 100 MB) at capture time; record `Truncated=true`.
- Check free disk before runs/exports; keep `MinimumFreeDiskBytes` free.

### 6.3 Network

- Reuse connections through `IHttpClientFactory` and `SocketsHttpHandler`.
- Bound `MaxConnectionsPerServer`; do not open a connection per job.
- Prefer `ResponseHeadersRead` and stream to disk.
- Dispose responses/content; do not rely on finalizers.
- Respect source caps and `Retry-After`; a retry storm is a ban risk.
- Use `ConnectTimeout` separate from request timeout.

### 6.4 CPU and allocations

- `Task.Run` is for CPU-bound work only; never for I/O.
- Use `Span<T>`/`Memory<T>` and `ArrayPool<byte>` in parsers and copy loops; return buffers in `finally`.
- Use `StringBuilder` with a capacity for large HTML/CSV assembly.
- Use `System.Text.Json` source generation for hot DTOs.
- Use `[LoggerMessage]` source-generated logging; avoid boxing and string interpolation.
- Avoid `GC.Collect()`; let the GC do its job. Measure before optimizing.
- API: server GC; CLI: workstation GC. Set in the project file, not at runtime.

### 6.5 RAM and backpressure

- Stream exports and large payloads; never materialize an unbounded result set.
- Bound every queue and channel; apply backpressure to producers.
- Cap page HTML, artifact size, and in-memory caches.
- Use `IAsyncEnumerable<T>` for streaming reads.
- Do not cache full job descriptions or HTML in memory; cache IDs and small DTOs.
- Monitor working set and GC heap; investigate growth over repeated runs, not one spike.
- `RecyclableMemoryStream` is optional and only after measuring allocation pressure.

### 6.6 Storage and cleanup

- Retention: artifacts 30 days, logs 90 days, inactive postings 180 days, backups per policy.
- Cleanup is idempotent and runs at startup and on a schedule; never delete the active run's files.
- Delete temp files older than 24 hours; delete orphaned run directories with no DB row only after a grace period.
- Log `BytesFreed`, `FilesDeleted`, and `SkippedActive` from cleanup.
- Check disk through the `IDiskSpace` port before writing; fail with `STORAGE_LOW_DISK` rather than filling the volume. Handlers do not call `DriveInfo` directly.
- Backups: `VACUUM INTO` or SQLite `.backup`, not a raw copy of a live WAL database.
- Windows: a file that is still open cannot be deleted; dispose before cleanup and surface the owning process.

### 6.7 SQLite

- WAL mode, `foreign_keys=ON`, `busy_timeout=5000`, one writer.
- One `DbContext` per unit of work; no long-lived tracking contexts.
- `AsNoTracking()` for reads; project to DTOs in the query.
- Use `ExecuteUpdateAsync`/`ExecuteDeleteAsync` for bulk maintenance instead of loading entities.
- Wrap multi-step writes in a transaction.
- `PRAGMA wal_checkpoint(TRUNCATE)` on shutdown/backup; `VACUUM` only in explicit maintenance.
- Pool connections; do not open/close a connection per row.
- A full disk must fail the transaction, not corrupt the DB; keep free space above the configured floor.

### 6.8 Browser and Playwright

- One browser process and one context per run; dispose page → context → browser.
- Stop tracing before disposing the context; an incomplete trace is not evidence.
- Close pages on challenge/stop paths; do not leave tabs open.
- Kill orphan browser processes only after the profile lock owner is confirmed dead.
- Do not share a user-data directory between concurrent runs.

---

## 7. Metrics, health, and verification

### 7.1 Metrics

Use `System.Diagnostics.Metrics`; OpenTelemetry export is optional and out of scope for v1.

| Metric | Type | Why |
|---|---|---|
| `joblens.runs.active` | UpDownCounter | Concurrency |
| `joblens.pages.fetched` | Counter | Volume/caps |
| `joblens.http.retries` | Counter | Upstream instability |
| `joblens.http.circuit_open` | Counter | API health |
| `joblens.http.fallbacks` | Counter | Stale-data exposure |
| `joblens.queue.depth` | ObservableGauge | Backpressure |
| `joblens.artifacts.bytes` | Counter | Disk growth |
| `joblens.disk.free_bytes` | ObservableGauge | Low-disk alarm |
| `joblens.log.async_buffer_used` | ObservableGauge | Dropped/blocked logs |
| `joblens.memory.working_set` | ObservableGauge | Leak detection |

### 7.2 Tests

- **Serilog:** use `Serilog.Sinks.InMemory` in tests; assert structured properties, `CorrelationId`, `TraceId`, redaction, and that no cookie/authorization value appears.
- **Global exceptions:** test `OperationCanceledException` is not logged as an error; test `500` contains `traceId` and no stack trace; test the CLI exit codes.
- **Polly:** use a fake `HttpMessageHandler`; assert retry counts, `Retry-After`, no retry for POST/4xx, circuit opening, timeout mapping, and fallback flags.
- **Cancellation:** assert `OperationCanceledException` propagates; no partial artifact is left behind; partial results are retained where specified.
- **Disposal:** count `DisposeAsync` calls; after a run, temp files are gone and the SQLite/log/profile handles are released; on Windows, delete the artifact directory to prove it.
- **Disk:** use an `IDiskSpace` port and a fake; do not call `DriveInfo` directly from handlers.
- **Soak:** 10 consecutive fixture runs; assert bounded file count, stable memory, no orphan processes, and no growing WAL.
- **Static analysis:** enable `CA1848` (LoggerMessage), `CA2000` (dispose), `CA2007` (ConfigureAwait), `CA2016` (CancellationToken), and `CA1031` (general catch) as warnings/errors in `.editorconfig`.

### 7.3 Profiling and verification

- `dotnet-counters monitor` for CPU, working set, GC heap, thread pool, exceptions.
- `dotnet-trace collect` for allocation/CPU hotspots; inspect with Speedscope/PerfView.
- `dotnet-gcdump` for leak confirmation.
- BenchmarkDotNet for parser/allocation changes, not for I/O-bound code.
- Measure before/after; do not optimize by intuition.

---

## 8. Anti-patterns

| Anti-pattern | Consequence | Fix |
|---|---|---|
| `new HttpClient()` per call | Socket exhaustion | `IHttpClientFactory` |
| Manual retry wrapped around Polly | Retry storms | One retry owner |
| Retrying POST/PUT/PATCH blindly | Duplicate writes | Idempotency key or disable |
| `Task.Run` around async I/O | Thread-pool waste | `await` directly |
| `.Result` / `.Wait()` | Deadlocks, thread starvation | Async all the way |
| Unbounded `Channel`/queue | Memory growth | `CreateBounded` + backpressure |
| `ReadAllBytes` on large artifacts | RAM spikes/LOH | Stream + `ArrayPool` |
| `GC.Collect()` | Pauses, no benefit | Remove; measure |
| Logging full HTML/response bodies | PII leak, disk blowup | IDs/paths/counts |
| String interpolation in logs | Lost structure, allocations | Named templates |
| Duplicate exception logging | Noise, missed root cause | Log once at boundary |
| `catch { }` | Silent failure | Log/return a typed error |
| Deleting active artifacts | Broken runs | Retention + active-run guard |
| Disposing injected `HttpClient` | Broken factory client | Do not dispose |
| `CancellationToken.None` in request paths | Hung requests | Propagate the token |
| `HttpClient.Timeout` infinite + no Polly cap | Hung sockets | Overall + per-attempt caps |

---

## 9. References

- [Serilog configuration](https://github.com/serilog/serilog/wiki/Configuration-Basics)
- [Serilog file sink and retention](https://github.com/serilog/serilog-sinks-file)
- [Serilog async sink](https://github.com/serilog/serilog-sinks-async)
- [AOT-Friendly Coding Policy](16-aot-and-trimming.md)
- [Microsoft: IHttpClientFactory](https://learn.microsoft.com/dotnet/core/extensions/httpclient-factory)
- [Microsoft: HTTP resilience with Polly](https://learn.microsoft.com/dotnet/core/resilience/http-resilience)
- [Polly v8 documentation](https://www.pollydocs.org/)
- [Microsoft: Async guidance](https://learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/)
- [Microsoft: Dispose and IAsyncDisposable](https://learn.microsoft.com/dotnet/standard/garbage-collection/implementing-disposeasync)
- [RFC 9457: Problem Details](https://www.rfc-editor.org/rfc/rfc9457)
