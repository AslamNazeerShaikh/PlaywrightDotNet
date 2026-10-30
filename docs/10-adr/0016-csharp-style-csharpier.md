# ADR-0016: C# style guides and csharpier formatting

- **Status:** Accepted
- **Date:** 2026-10-31
- **Deciders:** Project owner

## Context

The backend needs one C# style authority covering naming, organization, and
formatting, enforced automatically rather than by review memory. Three
references were adopted together.

## Decision

- **Naming and organization:** [ktaranov's C# standards](https://github.com/ktaranov/naming-convention/blob/master/C%23%20Coding%20Standards%20and%20Naming%20Conventions.md)
  and the [Google C# style guide](https://google.github.io/styleguide/csharpguide.html).
  They agree on the rules that matter here: `PascalCase` types/methods/
  properties/namespaces/files, `camelCase` locals and parameters,
  `_camelCase` private fields, `I`-prefixed interfaces, no underscores
  except the private-field prefix, file name matches the main class, one
  core class per file, `using` directives at the top with `System` first
  then alphabetical, attributes on their own lines, XML doc comments.
- **Formatting:** [csharpier](https://github.com/belav/csharpier) owns all
  whitespace, brace, and line-wrapping decisions. It is installed as a
  repo-local tool (`.config/dotnet-tools.json`, currently `1.3.0`).
- **Workflow:** run `dotnet tool run csharpier format .` from
  `src/backend/` before every commit; `csharpier check .` is part of the
  quality gates.

## Consequences

### Positive

- One deterministic format; no brace/indent debates in review.
- Analyzer-clean code stays clean: the guides' naming rules align with
  `TreatWarningsAsErrors` (CA1707/CA1716/CA1000-class issues surface as
  naming problems first).

### Negative / trade-offs

- Two deliberate deviations, documented here rather than drifted into:
  1. csharpier's 4-space indent and wrapping override Google's 2-space
     rule. The formatter is the tiebreaker for all layout.
  2. Namespaces are 3 levels (`JobScrape.<Layer>.<Feature>`) against
     Google's ≤2-level guidance. Layer-plus-feature grouping wins for a
     modular monolith; see the layout rule in
     [Solution & Tooling](../04-solution-and-tooling.md#101-naming).

### Follow-ups

- Add `csharpier check` to CI when the workflow lands.
- Revisit the pinned version monthly with the other toolchain upgrades.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| `dotnet format` only | Valid, but the owner chose csharpier for its deterministic, opinionated output; `dotnet format` remains available for analyzer-fix suggestions. |
| `.editorconfig` rules only, no formatter | Rules without a runner drift; csharpier is the runner. |
| Google 2-space indent | Conflicts with csharpier defaults and .NET ecosystem tooling; formatter wins. |

## References

- [Solution & Tooling §10](../04-solution-and-tooling.md#10-conventions)
- [ADR-0015](0015-jobscrape-naming-mtp-tests.md) (backend scaffold the formatter runs on)
