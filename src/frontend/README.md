# JobScrape Cockpit (control plane) — React 19 + Bun

> **Status:** walking skeleton. The BFF proxies `/api/*` loopback-only to the
> .NET data plane, and two cards exercise the tested backend features:
> engine health (`GET /api/v1/health`) and search-profile lookup
> (`GET /api/v1/search-profiles/{id}`).
>
> **Stack:** Bun `1.4.4` · React `19.3.1` · TypeScript `7.0.2` ·
> Tailwind CSS `4.3.3` · `bun test` + happy-dom + Testing Library.
> No Node.js, no npm, no Vite, no Prettier/Biome.

## Prerequisites

- Bun `1.4.4` (`bun --version`).
- The .NET API running loopback-only (defaults below assume it):
  `dotnet run --project src/backend/JobScrape.Api/JobScrape.Api.csproj`
  (serves `http://localhost:5207` in Development).

## Commands (run from `src/frontend/`)

| Command            | What it does                                              |
| ------------------ | --------------------------------------------------------- |
| `bun install`      | Resolve + install; writes `bun.lock` (committed).          |
| `bun run dev`      | BFF with HMR (`:3000`) + Tailwind watcher, in parallel.    |
| `bun run build`    | CSS → client bundle → server bundle, into `dist/`.        |
| `bun run start`    | Serve the production build (`NODE_ENV=production`).       |
| `bun test`         | 19 tests: config, proxy, routes, API client, components.   |
| `bun check`        | Typecheck (built into Bun, no `tsc` script).               |
| `bun run format` / `bun run format:check` | `bun fmt` (no Prettier/Biome).              |
| `bun run api:types`| Regenerate OpenAPI contract types (needs the API up).     |
| `bun audit`        | Dependency audit.                                          |

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
│   │   ├── index.html / main.tsx / app.tsx   # shell + health + profile cards
│   │   ├── lib/api.ts    # typed fetch helpers; UI maps stable codes, never prose
│   │   └── styles/       # Tailwind v4 CSS-first (globals.css in, generated.css out)
│   └── tests/
│       ├── server/       # config, proxy (incl. live stub upstream), routes
│       └── client/       # api client units, App interaction (stub client prop)
└── dist/                 # build output (gitignored)
```

## Conventions

- Same-origin only: the browser talks to the BFF; the BFF talks to
  `127.0.0.1`. CORS is never opened.
- Failures render the stable `code` (`PROFILE_NOT_FOUND`,
  `VALIDATION_FAILED`, `DOTNET_UNREACHABLE`, …), never prose parsing.
- `bun fmt` owns formatting; `bun check` owns types.

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
