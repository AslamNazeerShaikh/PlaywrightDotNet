# JobScrape Cockpit (control plane) — React 19 + Bun

> **Status:** ops-deck walking skeleton. The BFF proxies `/api/*` loopback-only
> to the .NET data plane, and four routed pages exercise the tested backend
> features: a central overview dashboard, engine-health detail,
> search-profile lookup, and a topology map.
>
> **Look:** hacker instrument-panel — matte-black surfaces, one emerald signal
> color, framed modules with corner brackets, IBM Plex Mono everywhere
> (self-hosted `src/client/fonts/`, offline-safe), tabular numerals, dense
> readouts that wrap instead of overflowing.
>
> **Stack:** Bun `1.4.4` · React `19.3.1` · TypeScript `7.0.2` ·
> Tailwind CSS `4.3.3` · `bun test` + happy-dom + Testing Library.
> No Node.js, no npm, no Vite, no Prettier/Biome.
> UI guidance: [ui-skills](https://www.ui-skills.com/skills) via the
> `ui-skills` MCP (`list_skills` → `get_skill`), per `AGENTS.md`.

## Prerequisites

- Bun `1.4.4` (`bun --version`).
- The .NET API running loopback-only (defaults below assume it):
  `dotnet run --project src/backend/JobScrape.Api/JobScrape.Api.csproj`
  (serves `http://localhost:5207` in Development).

## Commands (run from `src/frontend/`)

| Command                                   | What it does                                             |
| ----------------------------------------- | -------------------------------------------------------- |
| `bun install`                             | Resolve + install; writes `bun.lock` (committed).        |
| `bun run dev`                             | BFF with HMR (`:3000`) + Tailwind watcher, in parallel.  |
| `bun run build`                           | CSS → client bundle → server bundle, into `dist/`.       |
| `bun run start`                           | Serve the production build (`NODE_ENV=production`).      |
| `bun test`                                | 32 tests: config, proxy, routes, router, telemetry, API client, deck pages. |
| `bun check`                               | Typecheck (built into Bun, no `tsc` script).             |
| `bun run format` / `bun run format:check` | `bun fmt` (no Prettier/Biome).                           |
| `bun run api:types`                       | Regenerate OpenAPI contract types (needs the API up).    |
| `bun audit`                               | Dependency audit.                                        |

Copy `.env.example` to `.env` to override `PORT` (default `3000`) or
`DOTNET_API_URL` (default `http://127.0.0.1:5207`).

## Layout

```text
src/frontend/
├── package.json          # jobscrape-cockpit; scripts mirror docs/12-web-cockpit.md §8.1
├── bunfig.toml           # hoisted linker, minimumReleaseAge 3d, bun fmt policy
├── tsconfig.json         # bundler resolution, react-jsx, strict (per §8.4)
├── .env.example
├── src/
│   ├── server/
│   │   ├── index.ts      # Bun.serve: /healthz, /api/* proxy, SPA static
│   │   ├── config.ts     # pure env parsing (unit-tested)
│   │   └── proxy.ts      # target join, hop-by-hop filter, 502 mapping (unit-tested)
│   ├── client/
│   │   ├── index.html / main.tsx / app.tsx   # ops-deck shell: cmd bar, tabs, routed pages
│   │   ├── lib/
│   │   │   ├── api.ts        # timed fetch helpers; UI maps stable codes, never prose
│   │   │   ├── router.ts     # pure hash router: #/overview #/health #/profiles #/topology
│   │   │   └── telemetry.ts  # samples, stats, event log (pure, unit-tested)
│   │   ├── components/   # module frame, sparkline, pipeline diagram
│   │   ├── pages/        # overview, health, profiles, topology (one page per feature)
│   │   ├── fonts/        # IBM Plex Mono woff2, self-hosted (bundled as data URIs)
│   │   └── styles/       # design system: tokens, grid, modules, no-overflow rules
│   └── tests/
│       ├── server/       # config, proxy (incl. live stub upstream), routes
│       └── client/       # router, telemetry, api client units, deck interaction
└── dist/                 # build output (gitignored)
```

## Conventions

- Same-origin only: the browser talks to the BFF; the BFF talks to
  `127.0.0.1`. CORS is never opened.
- Failures render the stable `code` (`PROFILE_NOT_FOUND`,
  `VALIDATION_FAILED`, `DOTNET_UNREACHABLE`, …), never prose parsing.
- One page per feature, hash-routed (`#/overview`, `#/health`, `#/profiles`,
  `#/topology`): deep links survive refresh with zero server config.
- Dense but never overflowing: every grid child can shrink to zero, tokens
  wrap anywhere, wide tables scroll inside their module. Full viewport width
  is used; columns reflow at runtime via auto-fit grids plus a
  compact/cozy density switch in the command bar (cozy is default).
- Light paper deck by default, dark hacker instrument-panel on toggle
  (`THEME` in the command bar): every surface and diagram reads CSS vars, so
  the flip needs no markup changes.
- Diagrams are evidence: the pipeline and lifecycle SVGs reflect the latest
  probe outcome (unknown/live/down), and scale fluidly via `viewBox`.
- `bun fmt` owns formatting (including JSDoc reflow via `jsdoc = true`);
  `bun check` owns types.
- Motion is a system, not decoration: one spring token, staggered deck
  entrances, draw-in charts, breathing live-dots, press physics on controls
  (per `emilkowalski/animate` + `mengto/animation-systems`). Transform and
  opacity only; `prefers-reduced-motion` disables it all.
- UI taste follows ui-skills (`list_skills` → `get_skill` first):
  `tech-green-dark-mode-modern` for the signal system, `interface-design`
  for craft, `archify` for diagram rigor.

## Deliberate deviations from `docs/12-web-cockpit.md`

- **Port default is `5207`, not `5080`.** The doc's service table says
  `5080`; the repo's `launchSettings.json` serves `5207`. The BFF default
  follows repo reality; the table needs a fix-up.
- **No TanStack, no shadcn, no SSE yet.** Two fetches do not earn a query
  client (YAGNI); they land with the runs/jobs verticals, as do the
  `shadcn/ui` copy-in, auth (cookie/CSRF/bearer), and the SSE event proxy.
- **No `jest-dom`.** Its matchers have no `bun:test` type augmentation;
  the suite asserts on `textContent`. Re-add with proper setup if matchers
  earn their keep.
- **`@parcel/watcher` postinstall stays blocked.** One-shot Tailwind builds
  work without it; `dev:css --watch` will need
  `bun pm trust @parcel/watcher` with a PR reason when the loop demands it.
- **Favicon lives in `src/client/`, not `public/`.** Bun HTML imports
  resolve assets relative to the importing HTML; there is no `public/`
  convention. The `public/` folder was removed.

## Follow-ups (runs vertical)

Auth + CSRF + BFF bearer token, SSE run-event proxy, TanStack
Query/Router/Table, shadcn copy-in, `api:types` codegen into
`src/shared/contracts.ts`, `openapi-typescript` wiring, production
single-binary (`bun build --compile`) smoke.
