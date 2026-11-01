# JobScrape Cockpit (control plane) — React 19 + Bun

> **Status:** ops-deck walking skeleton. The BFF proxies `/api/*` loopback-only
> to the .NET data plane, and six routed pages exercise the tested backend
> features: a central overview dashboard, engine-health detail,
> search-profile management (create with inline server field errors,
> list, detail; persisted in SQLite), source status (compliance gate,
> daily quota meters, circuit state), browser-profile sessions (create,
> state and launch readiness), and a topology map.
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
| `bun test`                                | Config, proxy, routes, router, telemetry, API client, profile form, deck pages. |
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
│   │   │   ├── router.ts     # pure hash router: #/overview #/health #/profiles #/sources #/sessions #/topology
│   │   │   └── telemetry.ts  # samples, stats, event log (pure, unit-tested)
│   │   ├── components/   # module frame, sparkline, tac-map, scramble, legend,
│   │   │                 # compass, ruler-tabs (ArtCraft rail × radar blip)
│   │                 # (tac-map: god's-eye battlespace per iart-ai/svg-animation:
│   │                 #  sweep, tracers, blips — SMIL, motion-gated)
│   │   ├── pages/        # overview, health, profiles, sources, sessions, topology
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
- One page per feature, hash-routed (`#/overview`, `#/health`, `#/profiles`, `#/sources`, `#/sessions`,
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
- Render radar: every component logs `[render] Name #N` + `[mount]`/`[unmount]`
  via `useRenderLog`, and every API call logs `[fetch] GET path` — dev only
  (off in production builds and under `bun test`). Force on/off anywhere
  with `?renders=1` / `?renders=0`. Open devtools, filter the console, and
  runaway renders or remount loops show themselves immediately.
- Motion is a system, not decoration: one spring token, staggered deck
  entrances, draw-in charts, breathing live-dots, press physics on controls
  (per `emilkowalski/animate` + `mengto/animation-systems`). Transform and
  opacity only; `prefers-reduced-motion` disables it all.
- Elevation follows `mengto/beautiful-shadows`: layered neutral shadows,
  one strength per state (sm controls, md resting modules, lg lifted hover),
  recessed wells for data, borders never replaced. Top highlight included
  for dark-mode depth; light theme uses the exact recipe curves.
- UI taste follows ui-skills (`list_skills` → `get_skill` first):
  `tech-green-dark-mode-modern` for the signal system, `interface-design`
  for craft, `archify` for diagram rigor, `iart-ai/svg-animation` + 
  `mengto/css-border-gradient` for ambient motion.
- Ambient life: tri-color aurora drift, scan-beam sweep, shimmering brand,
  button sheens, sparkline sheen sweeps (SMIL, motion-gated).

## UI skill ledger

Every visual decision routes through the [ui-skills](https://www.ui-skills.com/skills)
registry first (`list_skills` → `get_skill` via the `ui-skills` MCP, per
`AGENTS.md`). What was consulted, and where it landed:

| Skill | Guided | Landed in |
| ----- | ------ | --------- |
| `mengto/tech-green-dark-mode-modern` | Signal system: matte-black surfaces, emerald accents, framed cards, mono labels | Design tokens, module frame, pills (`styles/globals.css`) |
| `dammyjay93/interface-design` | Craft bar: intent-first, domain-driven, no-default output | Ops-deck brief (dense/cold/terminal), page-per-feature IA |
| `tt-a1i/archify` | Diagram rigor: validated, code-backed flow diagrams | Topology lifecycle sequence, pipeline/tac-map diagrams |
| `emilkowalski/animate` | Motion build order: gate → purpose → cheapest tool (CSS), transform/opacity only | `Scramble`, entrance choreography, press physics |
| `mengto/animation-systems` | Motion tokens, stagger, choreography, reduced-motion policy | Spring/ease/duration tokens, staggered deck entrances |
| `mengto/beautiful-shadows` | Layered neutral elevation, one strength per state | Shadow tokens (sm/md/lg/well), hover lift |
| `iart-ai/svg-animation` | SMIL draw-on, motion paths, animated gradients | Sparkline draw + sheen, tac-map sweep + tracers |
| `mengto/css-border-gradient` | Quiet gradient edges, border-taste rules | Bracket/corner treatment kept quiet; borders never compete |
| `jakubkrehel/better-typography` | Type scale, wrapping, tabular numerals, input floors | Role scale (micro/label/body/title/display/stat), balance/pretty wraps, 16px touch inputs |
| `pbakaus/typeset` | Hierarchy/measure/role assessment before editing | Five-role audit; measure caps, per-role leading, synthesis guard |
| `ibelick/ui-skills-root` | Default router when no skill obviously applies | Pinned in `AGENTS.md`; consulted before each UI vertical |

## Design baseline

Default reference: **15.6-inch 1080p (1920 × 1080), 16:9, 141.21 PPI**.
The deck stays responsive and dynamic for every size, shape, ratio, and
orientation — but scale, density, and caps are tuned so this viewport is the
reference render, not an accident of it.

| Viewport (CSS px) | Layout | Type |
| ----------------- | ------ | ---- |
| 1920 (1080p @100%) | 12-col, full-bleed, 3-up modules | display 24, stat 20, title 13, body 12.5 |
| 1536 (1080p @125% — common OS scaling) | 12-col desktop holds | same caps as baseline |
| 1366 (laptop) | 12-col desktop holds | same caps as baseline |
| ≤1100 | spans collapse 3/4/5→6, 7/8→12 | fluid range active |
| ≤720 | 6-col grid, full-width rows, tighter padding | fluid minimums |
| 390 (phone) | single column; tables scroll in-module | minimums, no overflow |

Rules that follow from the baseline:

- Fluid type (`clamp`) bottoms out for small screens and **ceilings at the
  baseline** — 24px display / 20px stat never grow past 1920, so the
  reference render is stable and smaller screens degrade smoothly.
- No breakpoint between 1100 and 1920: the two most common 1080p
  configurations (100% and 125% scaling) share one desktop layout.
- Full-bleed is deliberate (`max-width: none`): the baseline's horizontal
  space is working space, filled by auto-fit clusters, never letterboxed.
- Verification is a live pass at 1920×1080 plus a narrow-width pass
  (390px): no horizontal page scroll, no clipped text, no overlapping
  modules — the grid's `minmax(0, 1fr)` + wrap-anywhere rules carry it.

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
