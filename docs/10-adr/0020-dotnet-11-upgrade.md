# ADR-0020: Target .NET 11

- **Status:** Accepted
- **Date:** 2026-11-17
- **Supersedes:** [ADR-0001](0001-dotnet-10-lts.md)

## Context

The machine carries only the .NET 11 SDK (`11.0.100`, runtime `11.0.0`), so the `net10.0` target no longer builds anywhere. The latest stable Microsoft packages are `11.0.0` across hosting, options, HTTP, EF Core, SQLite, OpenAPI, and test hosting. .NET 11 ships the hosting and logging abstractions inbox, so the explicit package references now fail restore (NU1510). Its analyzers add rule CA2026. The latest Scalar is `3.0.0`, and the latest csharpier is `1.4.0` with C# 15 support.

## Decision

Target **`net11.0`** in `Directory.Build.props` and every project, with SDK `11.0.100` pinned in `src/backend/global.json` (`rollForward: latestFeature`). Move the Microsoft packages to `11.0.0`, drop the two inbox abstraction references, and take the latest compatible third-party versions (`Scalar.AspNetCore 3.0.0`, `AngleSharp 1.10.4`, `coverlet.collector 10.1.1`, `Microsoft.NET.Test.Sdk 18.12.0`). Run the tree on csharpier `1.4.0`.

## Consequences

### Positive

- The backend builds and tests run on the only SDK installed.
- Two packages leave the tree because the framework ships them.
- Scalar 3.0.0 and csharpier 1.4.0 stay on their supported major lines.

### Negative / trade-offs

- .NET 11 is STS (short-term support), so it ends support sooner than the .NET 10 LTS baseline did. Revisit at the next LTS.
- Scalar 3.0.0 is a major bump. It compiles and the suites pass, but watch its Development-only reference page after each Scalar upgrade.
- The 16 browser-backed Infrastructure tests stay blocked until the §7.3 Playwright install runs on a machine. They fail on the missing browser binary, not on this upgrade.

### Follow-ups

- Re-check the pinned versions monthly with the .NET patch cadence.
- Add `csharpier check` to CI when the workflow lands (carried from ADR-0016).

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| Stay on `net10.0` without the SDK | Nothing builds. The SDK is gone and the pin cannot be satisfied. |
| Wait for the next LTS before moving | Blocks every backend task now for support runway later. The STS cost is known and scheduled. |
| Pin Scalar to the 2.x line | Keeps a known-good major but diverges from latest. The 3.0.0 build is green, so latest wins. |

## References

- [Solution & Tooling §4](../04-solution-and-tooling.md#4-sdk-and-build-configuration)
- [ADR-0001](0001-dotnet-10-lts.md) (superseded)
- [ADR-0016](0016-csharp-style-csharpier.md) (formatter ownership)
