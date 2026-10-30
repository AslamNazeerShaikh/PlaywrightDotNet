# ADR-0015: JobScrape solution naming, backend-first scaffold, native MTP tests

- **Status:** Accepted
- **Date:** 2026-10-30
- **Deciders:** Project owner

## Context

Phase 0 needs a buildable walking skeleton. The design docs specify a
`JobLens.slnx` solution with `JobLens.*` projects under `src/` and a `web/`
cockpit, but implementation starts backend-first: the .NET API must be built,
tested, and finalized before the React cockpit is layered on top. The .NET 10
SDK creates `.slnx` natively, enforces warnings-as-errors analysis on build,
and no longer supports the VSTest target for Microsoft Testing Platform test
applications.

## Decision

- Solution **`JobScrape.slnx`** lives in **`src/backend/`**; all .NET layers
  follow `<Feature>.JobScrape` inverted naming is rejected — projects are
  **`JobScrape.<Layer>`** (e.g. `JobScrape.Domain.csproj`,
  `JobScrape.Api.csproj`) so the assembly name reads naturally.
- `src/backend/` hosts the full .NET solution (Domain, Application,
  Infrastructure, Api + four test projects); `src/frontend/` hosts the React
  + Bun cockpit and stays a scoped placeholder until the backend health slice
  is green.
- API code uses **explicit `Program.Main` + block-scoped namespaces**; no
  top-level statements, no file-scoped namespaces.
- Tests run in **native MTP command mode**: `global.json` opts in via
  `"test": { "runner": "Microsoft.Testing.Platform" }`; commands use
  `dotnet test --solution/--project` with no bridge separator.
- Coverage for MTP uses **`Microsoft.Testing.Extensions.CodeCoverage`
  `18.12.0`** (`--coverage --coverage-output-format cobertura`); per-project
  files are merged with the repo-local `dotnet-coverage` `18.12.0` tool
  (`.config/dotnet-tools.json`). `coverlet.collector` stays referenced per
  the package matrix but is inert under native MTP.
- `FakeTimeProvider` is consumed from **`Microsoft.Extensions.Time.Testing`**
  (renamed namespace in the 10.x
  `Microsoft.Extensions.TimeProvider.Testing` package).

## Consequences

### Positive

- Backend and frontend evolve independently; each folder is self-describing
  with its own `.gitignore`.
- `Program.Main` keeps the composition root greppable and the code style
  uniform (every file uses block namespaces).
- Native MTP matches the SDK's direction; per-project coverage files merge
  deterministically instead of racing on one output path.

### Negative / trade-offs

- Project names (`JobScrape.*`) and paths (`src/backend`, `src/frontend`)
  diverge from the `JobLens.*` / `src/` + `web/` names in the design docs;
  doc updates reference both until the rename is applied repo-wide.
- One more test-only package (`Microsoft.Testing.Extensions.CodeCoverage`)
  beyond the matrix, pinned centrally like the rest.

### Follow-ups

- Apply the `JobScrape` rename repo-wide in design docs when the CLI slice
  lands (or record a superseding ADR if `JobLens` returns).
- Regenerate API types for the cockpit from `/openapi/v1.json` once the
  frontend scaffold starts.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| `JobLens.*` names under `src/` per docs §4 | Owner chose `JobScrape` naming and `src/backend` + `src/frontend` split; backend-first delivery needs the split now. |
| VSTest mode (`dotnet test <path>`) | Blocked: MTP test applications fail on .NET 10 SDK VSTest targets by design. |
| coverlet `--collect "Code Coverage"` | VSTest-only collector; inert under native MTP. |
| Profiler-based `dotnet-coverage collect` of test binaries | Profiler did not attach on this host; in-app MTP extension is deterministic. |

## References

- [Solution & Tooling](../04-solution-and-tooling.md)
- [Testing Strategy](../06-testing-strategy.md)
- [ADR-0002](0002-xunit-v3.md) (xUnit v3 executables are MTP applications)
- <https://aka.ms/dotnet-test-mtp-error>
