# ADR-0013: AOT-Friendly Code, No NativeAOT Release

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** Project owner

## Context

The project owner wants no AOT-hostile patterns in the codebase. The application will run normally on the CLR as an SDK-dependent .NET app; a full NativeAOT release is not required.

Two core dependencies block a full NativeAOT release:

- **Playwright for .NET** has no NativeAOT support today. It runs a Node sidecar and uses reflection-based JSON serialization; the upstream request is still open.
- **EF Core 10** supports NativeAOT only as a highly experimental feature with known trimming warnings and query limitations. Microsoft recommends against production use.

The owner explicitly wants to keep both. Removing them would remove browser automation and the relational model — a product change, not a portability improvement.

At the same time, several optional choices were reflection-heavy and avoidable:

- DataAnnotations options validation.
- `Serilog.Settings.Configuration` runtime binding.
- `Serilog.Exceptions`, `Serilog.Enrichers.AssemblyName`, and `Serilog.Enrichers.Demystifier`.
- Assembly-scanning/mediator-style registrations (already rejected in ADR-0011).
- Reflection-based JSON.

`ClosedXML` is also reflection-based, but it is a mature, trusted Excel writer and an explicit product choice. It is treated as an accepted vendor exception, alongside Playwright, EF Core, and Serilog.

## Decision

Adopt an **AOT-friendly coding policy** for our code; do not claim or gate on a NativeAOT release.

- No `System.Reflection`, `Activator`, `dynamic`, `Reflection.Emit`, assembly scanning, or runtime code generation in our assemblies.
- No `Expression.Compile()` in production; expression trees only for EF Core translation and test-only evaluation.
- JSON uses `System.Text.Json` source generation and reflection is disabled by default.
- Configuration uses the source-generated binder; options validation uses explicit `IValidateOptions<T>`.
- Logging uses explicit Serilog configuration, scalar properties, and no object destructuring or reflection enrichers.
- CLI uses `System.CommandLine` `SetAction`/`SetHandler`, never `CommandHandler.Create`/`NamingConventionBinder`.
- Excel export uses `ClosedXML`, isolated behind `IExportWriter`.
- DI uses explicit factory registrations; no assembly scanning.
- AOT/trim analyzers are enabled; warnings in our code are errors.
- A small `JobLens.AotSmoke` console (Domain/Application only) is published with `PublishAot=true` in CI to prove the core stays AOT-compatible.

Allowed vendor exceptions, isolated behind Infrastructure or the composition root:

| Dependency | Reason |
|---|---|
| Playwright | Required for browser automation; no NativeAOT support |
| EF Core | Required for persistence; NativeAOT experimental |
| Serilog | Explicit product requirement; restricted to the AOT-safe subset |
| ASP.NET Core hosting/DI | Framework; explicit registrations/source generators |
| AngleSharp | HTML parsing; AOT warning fixed upstream |
| System.CommandLine | AOT-supported with explicit handlers |
| ClosedXML | Trusted Excel writer; reflection-based; Infrastructure export only |
| Polly / Http.Resilience | AOT-friendly HTTP policies |

The full app's `PublishAot=true` is not a release gate and is expected to fail/warn because of Playwright and EF Core.

## Consequences

### Positive

- Our code stays portable and free of avoidable reflection.
- Trim/AOT analyzers catch new reflection-heavy code early.
- JSON, configuration, logging, and CLI paths become faster and more predictable.
- Removing DataAnnotations validation and Serilog reflection enrichers reduces dependency and warning surface.
- A future NativeAOT migration depends on upstream Playwright/EF Core support, not on rewriting our application logic.

### Negative / trade-offs

- Explicit factories and validators are more verbose than attribute/scanning-based registration.
- Source-generated JSON requires registering every DTO; forgetting one fails tests instead of silently using reflection.
- `System.CommandLine` parsing is more verbose without naming-convention binding.
- ClosedXML remains a reflection-based vendor exception; the export path is excluded from the AOT smoke and must stay behind `IExportWriter`.
- Serilog remains a vendor exception and may emit vendor trim warnings.
- Full NativeAOT remains blocked; the policy is not a promise of a native binary.

### Follow-ups

- Add `JobLens.AotSmoke` and publish it with `PublishAot=true` in CI.
- Add architecture tests banning reflection namespaces in Domain/Application.
- Add `JobLensJsonContext` and disable reflection-based JSON.
- Replace DataAnnotations options validation with `IValidateOptions<T>`.
- Keep `ClosedXML` behind `IExportWriter`; exclude the export path from AOT smoke.
- Remove reflection Serilog enrichers and `ReadFrom.Configuration`.
- Review new packages against this policy before adding them.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| Full NativeAOT now | Playwright has no support; EF Core NativeAOT is experimental and not production-ready. |
| Remove Playwright and EF Core | Removes browser automation and the persistence model; changes the product. |
| Status quo | Leaves avoidable reflection in options, JSON, logging, CLI, and Excel export. |
| AOT-friendly core + separate non-AOT worker | Possible later, but adds IPC/process complexity now; explicit policy gives most of the benefit. |
| Replace ClosedXML with `DocumentFormat.OpenXml` | Lower-level writer code; the product owner prefers the trusted ClosedXML API. |
| Suppress all AOT warnings | Hides real problems and defeats the purpose. |

## References

- [AOT-Friendly Coding Policy](../16-aot-and-trimming.md)
- [Runtime Hardening](../15-runtime-hardening.md)
- [EF Core NativeAOT (experimental)](https://learn.microsoft.com/ef/core/performance/nativeaot-and-precompiled-queries)
- [Playwright .NET AOT issue](https://github.com/microsoft/playwright-dotnet/issues/2714)
- [Serilog AOT issue](https://github.com/serilog/serilog/issues/1985)
- [System.CommandLine trimming/AOT](https://github.com/dotnet/command-line-api/issues/1776)
- [ADR-0011: No CQRS or MediatR](0011-no-cqrs-mediatr.md)
