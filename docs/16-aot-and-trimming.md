# AOT-Friendly Coding Policy

> **Target:** normal CLR, SDK-dependent .NET builds. JobLens is **not** published as NativeAOT.
>
> **Rule:** our code must not depend on reflection, runtime code generation, assembly scanning, or reflection-based serialization/configuration. AOT and trim analyzers must be clean for our assemblies.
>
> **Why:** the owner wants no AOT-hostile patterns in the codebase, even though Playwright and EF Core prevent a full NativeAOT release today.

---

## 1. Scope

This policy applies to:

- `JobLens.Domain`
- `JobLens.Application`
- `JobLens.Infrastructure`
- `JobLens.Cli`
- `JobLens.Api` (v2)
- all test projects

It does **not** claim that the full application can be published with `PublishAot=true`. It ensures that:

- our code has no avoidable reflection or dynamic-code dependency;
- trim/AOT analyzers stay clean for our assemblies;
- a future move to a more AOT-friendly browser/storage stack is a package swap, not a rewrite;
- vendor exceptions stay isolated at the Infrastructure/composition boundary.

See [ADR-0013](10-adr/0013-aot-friendly-policy.md) for the decision.

---

## 2. Hard rules for our code

| Banned | Use instead |
|---|---|
| `System.Reflection` (`Type.GetType`, `Assembly.GetTypes`, `MethodInfo.Invoke`, `PropertyInfo.GetValue/SetValue`) | Explicit types, explicit factories, generated code |
| `Activator.CreateInstance` / `ActivatorUtilities` | Explicit constructors and factories |
| `dynamic`, `System.Reflection.Emit`, runtime proxy generation | Static types |
| `Expression.Compile()` in production | Expression trees only for EF Core translation; compile only in tests |
| Assembly scanning (`Assembly.GetTypes`, Scrutor, MediatR registration) | Explicit DI registration in the composition root |
| `Newtonsoft.Json` and reflection-based `System.Text.Json` | `System.Text.Json` source generation (`[JsonSerializable]`) |
| `IConfiguration.Get<T>()` / `.Bind()` without the source generator | `EnableConfigurationBindingGenerator` + generated binding, or explicit reads |
| DataAnnotations validation (`ValidateDataAnnotations`) | Explicit `IValidateOptions<T>` checks |
| `CommandHandler.Create` / `System.CommandLine.NamingConventionBinder` | `SetAction` / `SetHandler` with explicit parse results |
| `AutoMapper`, `Mapster`, `Dapper`, `FluentValidation`, `MediatR` | Explicit mapping, ADO.NET/EF Core, explicit validation, explicit handlers |
| `ClosedXML` | `DocumentFormat.OpenXml` typed API |
| `System.ComponentModel.TypeDescriptor`, `Convert.ChangeType` | Typed parsing (`int.TryParse`, `Enum.TryParse<T>`) |
| `Enum.GetValues`/`GetNames` | Allowed; metadata access is AOT-safe |
| `Regex` constructed from runtime strings | `[GeneratedRegex]` |
| `Assembly.GetEntryAssembly()` for version | A generated `BuildInfo` constant (MSBuild `WriteLinesToFile`) |
| Serilog `ReadFrom.Configuration`, `{@Object}` destructuring, reflection enrichers | Explicit logger code, scalar properties only |

`typeof(T)`, `nameof(T)`, `Enum.TryParse<T>`, `Unsafe`, `MemoryMarshal`, `Span<T>`/`Memory<T>`, and generic type checks are AOT-safe. They are not "reflection" in the banned sense.

---

## 3. Expression trees and EF Core

Expression trees are allowed because EF Core needs them to translate queries and NativeAOT supports interpreted expressions:

```csharp
Expression<Func<JobPosting, bool>> predicate = job => job.Status == JobPostingStatus.Active;
```

Rules:

- No `Expression.Compile()` in production. It is test-only.
- No `Expression.Invoke` in EF queries; use a parameter-replacing visitor for composition.
- No dynamic LINQ (`System.Linq.Dynamic.Core`) or string-built expressions.
- Specifications expose `ToExpression()`; the read model hands the expression to EF Core.
- EF Core still uses reflection internally. That is a vendor exception (§4), not permission to add reflection elsewhere.

---

## 4. Vendor exceptions

These dependencies are retained by explicit product decision. They must stay behind Infrastructure or the composition root, and their types must not leak into Domain/Application.

| Dependency | AOT status | Containment / rule |
|---|---|---|
| **Playwright for .NET** | No NativeAOT support; Node sidecar and reflection-based JSON | Only `JobLens.Infrastructure.Sources`/`Browser`; never in Domain/Application. Do not attempt a NativeAOT publish of the host. |
| **EF Core 10 + SQLite** | NativeAOT is experimental and not production-ready; uses reflection | Only `JobLens.Infrastructure.Persistence`; use compiled models, explicit `IEntityTypeConfiguration`, typed LINQ, `AsNoTracking`; no dynamic LINQ. |
| **Serilog** | Not fully trim-annotated; reflection is used for configuration binding and object destructuring | Use the restricted subset: explicit code configuration, scalar properties, no `ReadFrom.Configuration`, no `{@...}`, no reflection enrichers. Isolate in the two hosts. |
| **ASP.NET Core hosting/DI** | Framework support with source generators; constructor activation may use reflection | Composition root only; explicit factory registrations; enable request-delegate/configuration source generators; no assembly scanning. |
| **AngleSharp** | Core parser fixed its AOT warning in 1.0.6; 1.8.6 is used | HTML parsing only; no scripting/CSS engine; keep in Infrastructure. |
| **System.CommandLine 2.0** | AOT/trimming supported when using explicit handler APIs | `SetAction`/`SetHandler` only; never `CommandHandler.Create` or `NamingConventionBinder`. |
| **DocumentFormat.OpenXml 3.x** | Trim-aware; reflection removed where possible | Typed SDK only; no schema reflection/validation on hot paths; keep in Infrastructure. |
| **Polly v8 / `Microsoft.Extensions.Http.Resilience`** | AOT-friendly | Configure pipelines in code; no reflection-based policy discovery. |

Vendor exceptions do **not** excuse reflection in our own code. If a vendor package needs a reflection-based feature, isolate it behind a port and document the exception in the PR.

---

## 5. Project and analyzer settings

`Directory.Build.props` (target):

```xml
<PropertyGroup>
  <EnableAotAnalyzer>true</EnableAotAnalyzer>
  <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
  <EnableSingleFileAnalyzer>true</EnableSingleFileAnalyzer>
  <SuppressTrimAnalysisWarnings>false</SuppressTrimAnalysisWarnings>
  <EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>
  <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
</PropertyGroup>
```

- `IsAotCompatible=true` on **Domain** and **Application** only. They have no vendor dependencies and should stay analyzer-clean.
- Domain/Application treat `IL2026`/`IL3050` as errors. Infrastructure may suppress them only at documented Playwright/EF Core boundaries, with a comment and a link to this policy.
- Do not set `PublishAot=true` on `JobLens.Cli`/`JobLens.Api`; Playwright and EF Core make that unsupported.
- `JsonSerializerIsReflectionEnabledByDefault=false` forces every DTO through a `JsonSerializerContext`.
- `EnableConfigurationBindingGenerator=true` makes `Bind`/`Get<T>` source-generated; do not fall back to reflection.

---

## 6. Serialization

All JSON uses source generation:

```csharp
[JsonSerializable(typeof(ScrapeRunSummary))]
[JsonSerializable(typeof(JobPostingDto))]
[JsonSerializable(typeof(ProblemDetails))]
internal sealed partial class JobLensJsonContext : JsonSerializerContext;
```

Rules:

- Pass `JobLensJsonContext.Default.ScrapeRunSummary` (or the generated type info) to `JsonSerializer`.
- No `JsonSerializer.Serialize(object)` or `Deserialize(object, Type)`.
- No `JsonSerializerOptions` without a `TypeInfoResolver`.
- API request/response types are registered in the context; OpenAPI generation uses the same types.
- `ProblemDetails`/`ValidationProblemDetails` are registered explicitly.
- Test serialization with the context, not the reflection fallback.

---

## 7. Configuration and options

- Enable the configuration binding source generator.
- Bind options with generated code; no `IConfiguration.Get<T>()` reflection fallback.
- Replace DataAnnotations with explicit `IValidateOptions<T>` checks.
- Keep options immutable (`init`).
- Secrets still come from user secrets/environment variables, never appsettings.

```csharp
public sealed class BrowserOptionsValidator : IValidateOptions<BrowserOptions>
{
    public ValidateOptionsResult Validate(string? name, BrowserOptions options)
    {
        if (options.NavigationTimeoutSeconds <= 0)
            return ValidateOptionsResult.Fail("NavigationTimeoutSeconds must be positive.");

        if (options.MaxHtmlBytes <= 0 || options.MaxHtmlBytes > 100 * 1024 * 1024)
            return ValidateOptionsResult.Fail("MaxHtmlBytes is out of range.");

        return ValidateOptionsResult.Success;
    }
}
```

No `[Required]`/`[Range]` attributes that depend on DataAnnotations reflection.

---

## 8. Logging

Serilog is retained as a vendor exception, but used in a restricted mode:

- Configure the logger in code; never `ReadFrom.Configuration` (reflection-based package binding).
- Push scalar properties (`RunId`, `Source`, `JobId`, `TraceId`, `CorrelationId`, counts, sizes).
- Never destructure objects with `{@...}`; Serilog's reflection path is then avoided on hot paths.
- No `Serilog.Exceptions`, `Serilog.Enrichers.AssemblyName`, or `Serilog.Enrichers.Demystifier` (removed; they rely on reflection/stack rewriting).
- Use a generated `BuildInfo.Version` constant from the MSBuild `Version` property instead of reading assembly attributes. Generate `BuildInfo.g.cs` in `obj/` with `WriteLinesToFile` and include it in `Compile`; no reflection.
- Exception logging uses the exception object and standard `ToString()` stack traces.
- AOT/trim warnings from Serilog are vendor warnings; do not suppress warnings in our code.

---

## 9. Dependency injection

- Register every service explicitly in the composition root.
- Prefer factory registrations:
  ```csharp
  services.AddSingleton<RunScrapeHandler>(sp => new RunScrapeHandler(
      sp.GetRequiredService<ISearchProfileRepository>(),
      sp.GetRequiredService<JobSourceRegistry>(),
      sp.GetRequiredService<CompliancePolicy>(),
      sp.GetRequiredService<TimeProvider>()));
  ```
- No assembly scanning, no convention-based registration, no `AddMediatR`, no `Scrutor`.
- Use named `HttpClient` instances plus an explicit client factory if typed-client activation warns under AOT analysis; do not rely on `ActivatorUtilities`.
- The DI container itself is a framework vendor exception; our registration code must not depend on reflection.

---

## 10. HTTP clients and resilience

- `IHttpClientFactory` named clients; configure handlers in code.
- `Microsoft.Extensions.Http.Resilience` (Polly v8) configured with explicit options; no reflection-based policy discovery.
- JSON source generation for request/response DTOs.
- No `HttpClient` per call; no `new HttpClient()`.
- Keep resilience code in Infrastructure.

---

## 11. CLI

- `System.CommandLine` `SetAction`/`SetHandler` only.
- Parse values explicitly with `ParseResult.GetValue(...)`.
- No `CommandHandler.Create`, no model binding by naming convention.
- Map `Result` errors to exit codes in one place.
- Do not use reflection to discover commands; build the command tree explicitly.

---

## 12. Excel export

`ClosedXML` is removed because it relies on reflection and other AOT-hostile machinery. Use `DocumentFormat.OpenXml` 3.5.1 with the typed API:

```csharp
using var document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
var workbookPart = document.AddWorkbookPart();
workbookPart.Workbook = new Workbook();
var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
worksheetPart.Worksheet = new Worksheet(new SheetData());
// build rows/cells explicitly; add hyperlink relationships explicitly
```

Rules:

- No schema validation/reflection on the export path.
- Stream rows; do not build the whole workbook in memory.
- Keep the writer behind `IExportWriter`.
- If OpenXML SDK emits trim/AOT warnings in our usage, replace it with a minimal `ZipArchive` + `XmlWriter` XLSX writer rather than adding a suppression.

---

## 13. Verification

- **Architecture test:** Domain/Application must not reference `System.Reflection`, `System.Reflection.Emit`, `System.Linq.Expressions.Expression.Compile`, or `dynamic`-related namespaces.
- **Analyzer gate:** build with AOT/trim analyzers; our code must be warning-free. Vendor warnings are tracked separately.
- **AOT smoke:** a small `JobLens.AotSmoke` console references Domain/Application (no Playwright/EF/Serilog) and publishes with `PublishAot=true` on macOS/Linux/Windows CI. It proves our core is AOT-compatible without claiming the full app is.
- **Serialization test:** every DTO round-trips through `JobLensJsonContext` with reflection disabled.
- **Options test:** generated binding + explicit validator; no DataAnnotations.
- **CLI test:** commands use `SetAction`; no `CommandHandler.Create`.
- **Review checklist:** reject new `System.Reflection`, `Activator`, assembly scanning, `dynamic`, `JsonSerializer` reflection, `ReadFrom.Configuration`, `{@...}`, or reflection-based packages.

The full app's `dotnet publish -p:PublishAot=true` is **expected to fail or warn** because of Playwright and EF Core. Do not use it as a release gate.

---

## 14. References

- [.NET Native AOT limitations](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
- [AOT-compatibility analyzers](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#aot-compatibility-analyzers)
- [EF Core NativeAOT (experimental)](https://learn.microsoft.com/ef/core/performance/nativeaot-and-precompiled-queries)
- [Playwright for .NET AOT issue](https://github.com/microsoft/playwright-dotnet/issues/2714)
- [Serilog AOT issue](https://github.com/serilog/serilog/issues/1985)
- [System.CommandLine trimming/AOT](https://github.com/dotnet/command-line-api/issues/1776)
- [AngleSharp AOT warning fix](https://github.com/AngleSharp/AngleSharp/issues/1144)
- [Open XML SDK trimming support](https://github.com/dotnet/Open-XML-SDK/blob/main/CHANGELOG.md)
- [Configuration binding source generator](https://learn.microsoft.com/dotnet/core/extensions/configuration-generator)
- [System.Text.Json source generation](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/source-generation)
