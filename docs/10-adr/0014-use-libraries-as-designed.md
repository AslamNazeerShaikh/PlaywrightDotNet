# ADR-0014: Use Libraries as Designed; AOT Is Not a Constraint

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** Project owner
- **Supersedes:** [ADR-0013](0013-aot-friendly-policy.md)

## Context

ADR-0013 made AOT friendliness a design driver. In practice, that meant:

- Serilog configuration was restricted to explicit code, with `ReadFrom.Configuration`, `Serilog.Exceptions`, and reflection enrichers removed.
- DataAnnotations options validation was replaced with hand-written validators.
- Scalar was treated as an AOT exclusion rather than a normal development tool.
- ClosedXML was replaced with a lower-level writer.
- An AOT smoke project and analyzer gates were added.

The application is not published as NativeAOT. It runs as a normal CLR, SDK-dependent .NET app. Playwright and EF Core are core dependencies and neither supports a production NativeAOT release. Restricting other libraries for AOT consistency adds custom code and removes documented, battle-tested behavior without a product benefit.

The owner wants libraries used in their intended, recommended way.

## Decision

Use each library according to its official documentation and recommended configuration. **AOT/trimming is not a design constraint.**

Concretely:

- Serilog uses `ReadFrom.Configuration`, standard enrichers, `Serilog.Exceptions`, and `Serilog.Enrichers.Demystifier`; object destructuring is allowed for small safe objects.
- Options use DataAnnotations plus `IValidateOptions<T>`, as documented by .NET.
- ClosedXML is the Excel writer.
- Scalar uses its normal ASP.NET Core integration and is Development-only because that is Scalar’s recommended/security-appropriate placement.
- `System.CommandLine` uses `SetAction`/`SetHandler` because that is the library’s recommended API; `CommandHandler.Create` remains out.
- EF Core and Playwright are used normally, behind Infrastructure ports for layering.
- JSON source generation and standard DI are allowed and encouraged where useful, but not mandated; reflection-based JSON and constructor injection are not banned.
- No AOT smoke project, AOT analyzer gate, or global reflection disable.

Architecture boundaries, runtime hardening, security, disposal, cancellation, and resource bounds remain in force. They are quality rules, not AOT rules.

## Consequences

### Positive

- Libraries behave as documented; fewer custom replacements.
- Serilog gets its full exception/enricher ecosystem and configuration model.
- DataAnnotations and ClosedXML remove hand-written workarounds.
- Scalar is used as designed rather than treated as an AOT exception.
- The build is simpler: no AOT analyzer gate or smoke project.

### Negative / trade-offs

- The app cannot be published as NativeAOT due to Playwright/EF Core (unchanged).
- Some libraries use reflection; that is accepted.
- Reflection-heavy code still needs normal review for performance, security, and clarity.
- Serilog/ClosedXML add transitive dependencies; review them normally.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| Keep ADR-0013's AOT restrictions | Removes documented library features without a product requirement; adds custom code. |
| Full NativeAOT | Playwright unsupported; EF Core experimental; would change the product. |
| Allow AOT to drive package choices but not code | Still removes useful libraries/features for a non-goal. |
| No architecture boundaries | Rejected; layering is for testability and change isolation, not AOT. |

## References

- [Library Usage Policy](../16-library-usage-policy.md)
- [Runtime Hardening](../15-runtime-hardening.md)
- [Application Patterns](../14-application-patterns.md)
- [ADR-0013: AOT-friendly policy](0013-aot-friendly-policy.md) — superseded
