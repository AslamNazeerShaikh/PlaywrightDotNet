# ADR-0010: React + Bun Web Cockpit (No Node.js)

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** Project owner
- **Refines:** [ADR-0008](0008-cli-first-api-later.md) (CLI first, API later) — the CLI remains the v1 interface; the API is promoted to the first **v2.0** deliverable because the cockpit depends on it.

## Context

The .NET engine is the data plane: Playwright sessions, source adapters, third-party API clients, parsing, persistence, quotas, and compliance policy. The operator needs a **central control system** — a cockpit — to see health, manage search profiles, start/cancel runs, watch live progress, inspect challenges, browse jobs, export data, and run diagnostics.

The project is local-first, single-user, and cross-platform. The existing plan is CLI-first with an API/dashboard deferred. The cockpit changes the timing: an HTTP API is now a prerequisite for the v2.0 control plane, not a distant “maybe”.

The owner requires:

- React + Tailwind + TypeScript + shadcn/ui.
- Bun as the runtime, package manager, bundler, dev server, test runner, formatter, type checker, and compiler.
- **No Node.js** on the machine.
- Latest stable versions of the frontend stack.

## Decision

Build the cockpit as a **client-rendered React SPA** served by a **same-origin Bun BFF**:

- **UI:** React `19.3.1`, TypeScript `7.0.2`, Tailwind CSS `4.3.3`, shadcn/ui CLI `4.23.0`, TanStack Query/Router/Table, React Hook Form + Zod, Recharts, Sonner.
- **Runtime:** Bun `1.4.4` for install (`bun install`/`bun ci`), execution (`bun run`, `bun --hot`), serving (`Bun.serve`), bundling (`bun build`), testing (`bun test`), formatting (`bun fmt`), type checking (`bun check`), and compilation (`bun build --compile`).
- **Server:** Bun BFF proxies `/api/v1/*` and `/events/*` to the loopback .NET API, injects the API bearer token, serves the SPA, owns the auth cookie/CSRF, and normalizes SSE.
- **Transport:** HTTP/JSON commands + Server-Sent Events for live updates; no WebSocket in v2.0.
- **Styling:** Tailwind v4 CSS-first configuration; official `@tailwindcss/cli` for CSS build/watch, because Bun’s native Tailwind plugin is not the official Tailwind release and can lag the pinned version.
- **Components:** shadcn CLI copies component source into the repo; components are reviewed and pinned by CLI version.
- **Deployment:** local processes, or a single Bun-compiled binary for the BFF; static assets ship beside it if not embedded.

The browser never calls the .NET API or any job site directly. All scraping, policy, and challenge decisions remain in .NET.

## Consequences

### Positive

- One language and one toolchain for the frontend; no Node, no Vite, no webpack, no Jest/Vitest, no Prettier/Biome, no `ts-node`.
- Bun’s built-in formatter, type checker, test runner, and compiler remove four or five dependencies and their configs.
- Same-origin BFF removes CORS and keeps the API token out of the browser.
- SSE gives a live cockpit without the complexity of bidirectional sockets.
- shadcn components are owned source, not an opaque dependency; the UI can be made dense and keyboard-first.
- The .NET engine stays independently testable and runnable from the CLI.

### Negative / trade-offs

- Bun is younger than Node; some ecosystem packages assume Node internals. W0 must prove the toolchain.
- Bun’s native Tailwind plugin is not used; the project runs the official Tailwind CLI in parallel, adding one process in development.
- shadcn is copy-in code; upgrades are manual diffs, not a package bump.
- A BFF is another process and another place for auth bugs; it must be small, tested, and loopback-only.
- The cockpit requires the .NET API earlier than ADR-0008 implied; v2.0 scope grows.
- Single-binary asset embedding needs a spike; the fallback is shipping `dist/client` beside the binary.

### Follow-ups

- W0 spike: Bun + React + Tailwind + shadcn + SSE + single binary, with no Node installed.
- Define the .NET API contract and OpenAPI document; generate `src/shared/contracts.ts`.
- Implement auth, CSRF, CSP, and loopback binding before any command endpoint.
- Add the cockpit track to the delivery plan (W0–W4) and keep v1.0 CLI scope unchanged.
- Pin exact versions, commit `bun.lock`, and run monthly upgrade PRs.
- Revisit WebSocket only if bidirectional terminal/log interaction becomes a real requirement.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| Next.js / Remix | Requires Node-oriented tooling and a server runtime that conflicts with the Bun-only policy; SSR adds no value for a local cockpit. |
| Vite + React | Excellent DX, but adds Vite/Node tooling that Bun’s native bundler and HTML imports replace. |
| Bun + `bun-plugin-tailwind` | Not the official Tailwind package and can lag the pinned Tailwind version; official CLI is predictable. |
| SvelteKit / Vue | Viable, but the owner chose React + shadcn and the team’s component vocabulary is React. |
| Blazor Server/WASM | Keeps one language but loses the requested React/shadcn ecosystem and adds SignalR hosting complexity. |
| ASP.NET Core Razor Pages | Simple hosting, but a poorer fit for a dense, interactive cockpit and the requested component stack. |
| Electron/Tauri desktop app | Packaging and update overhead; a local web app already runs in the browser. |
| Browser → .NET API directly | Requires CORS, exposes the API token to the browser, and duplicates auth/CSRF logic. |

## References

- [Web Cockpit specification](../12-web-cockpit.md)
- [Architecture](../02-architecture.md)
- [CLI-first ADR](0008-cli-first-api-later.md)
- [Bun 1.4.4](https://bun.com/blog/bun-v1.4.4)
- [Bun fullstack dev server](https://bun.com/docs/bundler/fullstack)
- [Bun type checking](https://bun.com/docs/runtime/check)
- [Bun formatter](https://bun.com/docs/format)
- [shadcn/ui](https://ui.shadcn.com/docs/components)
- [Tailwind CSS v4](https://tailwindcss.com/docs)
