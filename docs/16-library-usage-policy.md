# Library Usage Policy

> **Principle:** use every library in its documented, recommended, battle-tested way.
>
> **AOT is not a design constraint.** Do not remove, avoid, or water down a library feature because it uses reflection, runtime code generation, or is not NativeAOT/trimming compatible. The application runs as a normal CLR, SDK-dependent .NET app.

---

## 1. Rules

1. **Follow the library’s official documentation first.** Prefer its default/recommended setup over a hand-rolled replacement.
2. **Do not restrict a library because of AOT/trimming.** Reflection-based configuration, serialization, mapping, validation, and convenience APIs are allowed when they are the library’s intended interface.
3. **Keep architecture boundaries for design reasons, not AOT.** Vendor types stay behind ports/adapters so the Domain and Application stay testable and independent. That is a layering rule, not a trimming workaround.
4. **Use the framework’s recommended features.** Source-generated JSON and source-generated logging are welcome when they improve performance or ergonomics, but they are optional, not mandatory.
5. **Security, privacy, disposal, cancellation, and resource bounds still apply.** Those are runtime-hardening rules, not AOT restrictions.
6. **Prefer the library’s own extension points.** Do not build a parallel abstraction that hides the library’s normal API unless there is a concrete testability or portability requirement.

---

## 2. Library-specific guidance

### Serilog

Use Serilog’s standard configuration model:

- `ReadFrom.Configuration(...)` with the `Serilog` section in `appsettings.json`.
- `FromLogContext`, machine/environment, process, thread, span, assembly, and exception enrichers.
- `Serilog.Exceptions` and `Serilog.Enrichers.Demystifier` for detailed exception/stack-trace data.
- Object destructuring (`{@Object}`) for small, safe objects; never full HTML, responses, cookies, or auth headers.

The full pipeline is in [Runtime Hardening §2](15-runtime-hardening.md#2-structured-logging-with-serilog).

### Options and validation

Use DataAnnotations for simple ranges plus `IValidateOptions<T>` for cross-field rules, exactly as the .NET options documentation shows:

```csharp
public sealed class BrowserOptions
{
    [Required]
    public string DefaultEngine { get; init; } = "Chromium";

    [Range(1, 300)]
    public int NavigationTimeoutSeconds { get; init; } = 45;
}

builder.Services
    .AddOptions<BrowserOptions>()
    .Bind(builder.Configuration.GetSection(BrowserOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

Source-generated configuration binding is allowed and recommended for performance, but the standard reflection-based binder is not forbidden.

### ClosedXML

Use `ClosedXML` as the Excel writer:

```csharp
using var workbook = new XLWorkbook();
var sheet = workbook.Worksheets.Add("Jobs");
sheet.Cell(2, 1).Value = "Senior .NET Engineer";
sheet.Cell(2, 1).SetHyperlink(new XLHyperlink(applyUrl));
workbook.SaveAs(path);
```

Keep it behind `IExportWriter` for testability, bound the row count, dispose the workbook, and write atomically. Do not replace it with a lower-level writer merely for AOT.

### Scalar

Use Scalar’s documented ASP.NET Core integration:

```csharp
builder.Services.AddOpenApi();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("JobLens API v1");
    });
}
```

Development-only is Scalar’s recommended and security-appropriate placement, not an AOT restriction. Use the default UI features; privacy hardening such as `DisableAgent()`/`DisableDefaultFonts()` is an optional product choice.

### System.CommandLine

Use the API the library recommends: `SetAction`/`SetHandler` with explicit parse results. `CommandHandler.Create`/`NamingConventionBinder` is legacy, reflection-heavy, and moved to a separate package; this is a library recommendation, not an AOT restriction.

### EF Core and Playwright

Use both normally:

- EF Core: `DbContext`, LINQ, migrations, compiled models where useful, explicit configurations, projections, `AsNoTracking`.
- Playwright: normal browser/context/page lifecycle, tracing, locators, persistent profiles.

Keep their types inside Infrastructure for layering, not because of AOT.

### JSON and dependency injection

- `System.Text.Json` source generation is preferred for hot DTOs and APIs, but reflection-based serialization is allowed where it is simpler and not on a hot path.
- Constructor injection and standard DI registration are fine. Assembly scanning is allowed if a library’s recommended setup uses it; do not add a scanner only to replace explicit code.
- Do not disable reflection globally (`JsonSerializerIsReflectionEnabledByDefault=false`) unless a concrete requirement justifies it.

---

## 3. What we still enforce

These are quality rules, not AOT rules:

- No secrets, cookies, auth headers, or personal data in logs/artifacts.
- `Result<T>` for expected failures; exceptions for programmer/invariant errors.
- `CancellationToken` on every I/O boundary; no sync-over-async.
- `using`/`await using`; bounded queues; streamed exports; disk-space checks.
- Explicit tests for error paths, disposal, cancellation, and retries.
- Architecture tests for layer boundaries, not for reflection bans.

---

## 4. Reviewing a new library

Check:

- official documentation and recommended usage;
- license and maintenance status;
- .NET 10 compatibility;
- security posture and transitive dependencies;
- whether it fits an existing port rather than leaking through layers.

Do **not** reject a library because it is not NativeAOT-compatible.

---

## 5. Related decisions

- [ADR-0014: Use libraries as designed; AOT is not a constraint](10-adr/0014-use-libraries-as-designed.md)
- [ADR-0013: AOT-friendly policy](10-adr/0013-aot-friendly-policy.md) — superseded
- [Application Patterns](14-application-patterns.md)
- [Runtime Hardening](15-runtime-hardening.md)

## 6. References

- [Serilog configuration](https://github.com/serilog/serilog/wiki/Configuration-Basics)
- [Serilog.Exceptions](https://github.com/RehanSaeed/Serilog.Exceptions)
- [ClosedXML](https://github.com/ClosedXML/ClosedXML)
- [Scalar ASP.NET Core integration](https://scalar.com/products/api-references/integrations/aspnetcore/integration)
- [Options pattern](https://learn.microsoft.com/dotnet/core/extensions/options)
- [EF Core](https://learn.microsoft.com/ef/core/)
- [Playwright for .NET](https://playwright.dev/dotnet/)
