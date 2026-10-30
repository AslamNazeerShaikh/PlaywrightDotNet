# ADR-0017: XML documentation as a global build rule

- **Status:** Accepted
- **Date:** 2026-10-31
- **Deciders:** Project owner

## Context

Sparse comments caused real confusion about core types (the `Result`
factory hub needed a verbal explanation that belonged in the code).
Review memory does not scale; the compiler does.

## Decision

- **Every public type and member in production code carries XML
  documentation** (`summary`, plus `param`/`returns`/`exception` where
  applicable). Private logic that is not self-evident carries `//`
  comments explaining *why*, not *what*.
- **Enforcement is mechanical:** `Directory.Build.props` sets
  `<GenerateDocumentationFile>true</GenerateDocumentationFile>`, so
  missing documentation (CS1591) fails the build through the existing
  global `TreatWarningsAsErrors` rule. No warning-suppression markers are
  used to satisfy it; causes are fixed (rename, restructure, document).
- **Test projects are explicitly exempt** (`GenerateDocumentationFile`
  set to `false` in each test `.csproj` with a comment): test names state
  the expectation, and examples live in
  [Testing Strategy](../06-testing-strategy.md).

## Consequences

### Positive

- The next reader never has to ask what a type is for; the answer is one
  hover away in the IDE and in IntelliSense for API consumers.
- Doc drift is caught the same way as any other warning: at build time.

### Negative / trade-offs

- More lines to maintain per type. Accepted: documentation is part of done
  (see the Definition of Done), and csharpier keeps the formatting uniform.
- `cref` and `param` names must stay accurate; the compiler checks them
  (CS1572/CS1574) under the same rule.

### Follow-ups

- Revisit if documentation coverage tooling (beyond CS1591 presence
  checks) becomes available.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| Comment-convention document only | Unenforced conventions decay; the compiler does not forget. |
| Suppress-and-exempt per file (`#pragma`, `NoWarn`) | Banned by project policy: suppressions hide causes instead of fixing them. |
| XML docs on tests too | Noise; test names plus the testing-strategy examples already serve that role. |

## References

- [Solution & Tooling §10](../04-solution-and-tooling.md#10-conventions)
- [ADR-0016](0016-csharp-style-csharpier.md) (style guides and formatter)
