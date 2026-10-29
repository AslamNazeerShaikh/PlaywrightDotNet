# ADR-0001: Target .NET 10 LTS

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** Project owner

## Context

The solution must run on macOS, Linux, and Windows; be maintainable for years; and use a runtime the owner already knows. The machine has .NET SDK `10.0.403` and runtime `10.0.14`. .NET 10 is an LTS release supported until 2028-11-14. .NET 9 is STS and ends support on 2026-11-10; .NET 8 is LTS but also ends support on 2026-11-10. .NET 11 is in RC and not a stable target.

## Decision

Target **`net10.0`** with SDK `10.0.403` pinned in `global.json` (`rollForward: latestFeature`). Stay current with monthly .NET 10 patches.

## Consequences

### Positive

- Supported through November 2028.
- Modern C#/BCL features (`TimeProvider`, rate limiting, improved async/DI).
- Cross-platform, self-contained publishing.
- Aligns with the owner's ASP.NET Core experience.

### Negative / trade-offs

- Some third-party tools may lag `net10.0` support; verify at each upgrade.
- Playwright's OS support is stricter than .NET's (Windows 11+, macOS 14+), so the product matrix is the intersection.

### Follow-ups

- Update the SDK pin monthly after CI passes.
- Keep `InvariantGlobalization=false` for locale/timezone correctness.
- Revisit only for a security/lifecycle reason, not novelty.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| .NET 9 | STS; ends support 2026-11-10. |
| .NET 8 | LTS but ends support 2026-11-10; no reason to start there. |
| .NET 11 RC | Pre-release; not a stable baseline. |
| .NET Framework | Not cross-platform/modern. |

## References

- <https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core>
- [Research Analysis](../01-research-analysis.md)
