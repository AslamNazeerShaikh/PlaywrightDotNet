# JobScrape Cockpit (control plane) — React 19 + Bun

> **Status:** ops-deck live. The BFF proxies `/api/*` loopback-only
> to the .NET data plane with a server-side session (HttpOnly cookie, CSRF
> check, session bearer; e2e-tested), and eleven routed pages exercise the
> tested backend: a central overview dashboard, engine-health detail,
> search-profile management (create with inline server field errors,
> list, detail; persisted in SQLite), source status (compliance gate,
> daily quota meters, circuit state), browser-profile sessions (create,
> state and launch readiness), runs (start a scrape, follow it live by
> polling, cancel it, read its postings), a topology map, database tables,
> users, roles, and quotas (limit + window edits).
>
> **Look:** hacker instrument-panel — matte-black surfaces, one emerald signal
> color, framed modules with corner brackets, IBM Plex Mono everywhere
> (self-hosted `src/client/fonts/`, offline-safe), tabular numerals, dense
> readouts that wrap instead of overflowing.
>
> **Stack:** Bun `1.4.4` · React `19.3.1` · TypeScript `7.0.2` ·
> Tailwind CSS `4.3.5` · `bun test` + happy-dom + Testing Library.
> No Node.js, no npm, no Vite, no Prettier/Biome.
> UI guidance: [ui-skills](https://www.ui-skills.com/skills) via the
> `ui-skills` MCP (`list_skills` → `get_skill`), per `AGENTS.md`.

## Prerequisites

- Bun `1.4.4` (`bun --version`).
- The .NET API running loopback-only on its `http` profile — the default, no
  cert and no admin step:
  `dotnet run --project src/backend/JobScrape.Api/JobScrape.Api.csproj --launch-profile http`
  serves `http://localhost:5207`.
- Optional: the [HTTPS dev loop](#https-dev-loop-optional) is the alternate
  lane (`https://localhost:5208` plus an https BFF) and needs a one-time
  dev-cert trust; nothing in the default path below touches certs.

Quick start (plain HTTP, zero admin steps):

```bash
# terminal 1 — API on http://localhost:5207
dotnet run --project src/backend/JobScrape.Api/JobScrape.Api.csproj --launch-profile http

# terminal 2 — BFF on http://127.0.0.1:3000
cd src/frontend && bun run dev:http
```

Open `http://127.0.0.1:3000/#/overview`. `dev:http` pins
`DOTNET_API_URL=http://127.0.0.1:5207` and sets no `NODE_EXTRA_CA_CERTS`, so a
fresh clone needs no `.env` at all. If you do copy `.env.example`, use its
commented http-only block: the active block there is the https lane, whose
`BFF_TLS_*` paths would otherwise make the BFF look for cert files.

Verify: `curl http://localhost:5207/api/v1/health` (API),
`curl http://127.0.0.1:3000/api/v1/health` (BFF proxy, must return `healthy`,
which also proves Bun booted without a CA file).

## Commands (run from `src/frontend/`)

| Command                                   | What it does                                             |
| ----------------------------------------- | -------------------------------------------------------- |
| `bun install`                             | Resolve + install; writes `bun.lock` (committed).        |
| `bun run dev:http`                        | Default lane: BFF with HMR on `http://127.0.0.1:3000`, pinned to `http://127.0.0.1:5207`; no cert, no `.env`. Run `bun run dev:css` alongside when editing styles. |
| `bun run dev`                             | HTTPS lane: BFF with HMR (`:3000`, HTTPS) + Tailwind watcher, in parallel. Needs the optional cert setup below; no manual export (scripts carry `NODE_EXTRA_CA_CERTS`). |
| `bun run build`                           | CSS → client bundle → server bundle, into `dist/`.       |
| `bun run start`                           | Serve the production build (`NODE_ENV=production`).      |
| `bun test`                                | Config, proxy, routes, router, telemetry, API client, profile form, runs page, deck pages. |
| `bun run test:e2e`                        | Playwright suite (`src/tests/e2e/*.e2e.ts`). Boots its own temp backend + temp DB and BFF on loopback ports (`:5399` / `:3399`), drives Playwright's bundled Chromium (install once with `bunx playwright install chromium`), and deletes the temp root after the run. The shared dev backend and its database are never touched. |
| `bun run test:flags`                      | Flag-off validation (`src/tests/e2e/validate-flags.ts`): boots the backend twice on temp ports + temp DBs, proves the harness on the default boot, then pins health-404, features payload, and csv-404/xlsx-present with Health + CsvExport off. Cleans up its temp dirs and processes. |
| `bun run test:pretest`                    | Pre-test live validation (`src/tests/e2e/validate-pretest.ts`): boots a temp backend + seeded admin, then pins the login/setup/lockout/role/rate-limit/idempotency/quota/MCP matrices and the `PASSWORD_SETUP_COMPLETE`, `ACCOUNT_LOCKED`, `IDEMPOTENCY_KEY_REUSED`, `QUOTA_EXCEEDED` codes over HTTP, including quota counter resets across restarts. Cleans up on every exit path; nonzero exit on any failure. |
| `bun check`                               | Typecheck (built into Bun, no `tsc` script).             |
| `bun run format` / `bun run format:check` | `bun fmt` (no Prettier/Biome).                           |
| `bun run api:types:http`                  | Regenerate OpenAPI contract types from the http lane (`:5207`); no cert. |
| `bun run api:types`                       | Regenerate OpenAPI contract types from the https lane (needs the dev cert). |
| `bun audit`                               | Dependency audit.                                        |

Copy `.env.example` to `.env` to override `PORT` (default `3000`) or
`DOTNET_API_URL` (default `https://localhost:5208`, the https lane). The file
also carries a commented http-only block (`http://127.0.0.1:5207`, TLS lines
commented); the http lane works without any `.env` at all.

### End-to-end

`bun run test:e2e` is self-contained: it boots a temp backend (temp SQLite
DB, temp artifacts/logs) and the BFF on loopback ports `:5399` and `:3399`
(override with `JOBSCRAPE_E2E_API_PORT` / `JOBSCRAPE_E2E_BFF_PORT`), then
drives Playwright's bundled Chromium against the pair. The suite never reads
the shared dev DB, and the temp root is removed on teardown. It needs
`dotnet`, `bun`, and a one-time `bunx playwright install chromium`; no
running dev servers and no system Chrome are required. If a crashed
run leaves the temp root behind, the next run reuses its seeded admin and
finishes setup idempotently. Existing specs pin the sign-in gate, anonymous
features flags, database/runs guards, and the auth flows: first-login setup
surviving a reload, a wrong current password keeping the session, the
HttpOnly session cookie (no bearer in JS storage), and a role change signing
the affected user out.

`bun run test:flags` needs only `dotnet` on PATH: it boots its own backends
on temp ports with temp databases and deletes both afterwards.

`bun run test:pretest` is the PR 6 live matrix: one temp backend plus seeded
admin, then HTTP checks for the unified 401 oracle, `PASSWORD_SETUP_COMPLETE`,
`ACCOUNT_LOCKED`, rate-limit `Retry-After`, idempotency replay/reuse, quota
429s and counter resets across host restarts, MCP anonymous/pending/no-perm
denials, the shipped default seed, and startup rejection of the demo secret.
It also cleans up on SIGINT/SIGTERM and exits nonzero on any failed check.

## HTTPS dev loop (optional)

Optional alternate lane; the default path above needs no cert. This lane keeps
both hops on TLS: the API serves `https://localhost:5208` (its `https`
applicationUrl in `src/backend/JobScrape.Api/Properties/launchSettings.json`),
the BFF proxies to it, and the deck itself is served over
`https://127.0.0.1:3000` using the same dev cert (its SANs cover `localhost`
and `127.0.0.1`). Bun does not read the macOS keychain, so the dev cert must
be trusted **and** exported. One-time setup per machine (run from the repo
root):

```bash
# 1. Trust the .NET HTTPS dev cert (may prompt for a keychain password).
dotnet dev-certs https --trust

# 2. Export its public cert (BFF -> API trust) ...
mkdir -p src/frontend/.certs
dotnet dev-certs https --export-path src/frontend/.certs/devcert.pem \
  --format Pem --no-password

# 3. ... plus a cert/key pair so the BFF can serve HTTPS itself.
# Password is a throwaway: the files stay gitignored on this machine.
dotnet dev-certs https --export-path src/frontend/.certs/localhost.pfx \
  --format Pfx -p jobscrape-dev-only
openssl pkcs12 -in src/frontend/.certs/localhost.pfx -clcerts -nokeys \
  -out src/frontend/.certs/localhost-cert.pem -passin pass:jobscrape-dev-only
openssl pkcs12 -in src/frontend/.certs/localhost.pfx -nocerts -nodes \
  -out src/frontend/.certs/localhost-key.pem -passin pass:jobscrape-dev-only
rm src/frontend/.certs/localhost.pfx
chmod 600 src/frontend/.certs/localhost-key.pem

# 4. Copy env and start both servers.
cp src/frontend/.env.example src/frontend/.env
dotnet run --project src/backend/JobScrape.Api/JobScrape.Api.csproj --launch-profile https
cd src/frontend && bun run dev
```

`bun run dev` needs no manual export: `dev:server` and `api:types` bake in
`NODE_EXTRA_CA_CERTS=./.certs/devcert.pem` (Bun builds its CA store before
`.env` loads, so a `.env`-only value would leave the proxy at
`502 DOTNET_UNREACHABLE`).

Verify: `curl https://localhost:5208/api/v1/health` (API),
`curl https://127.0.0.1:3000/api/v1/health` (BFF proxy, must return
`healthy`), and open `https://127.0.0.1:3000/#/overview` in the browser —
Chromium-based browsers and Safari accept it via the keychain trust from step 1. Firefox uses
its own cert store: import `localhost-cert.pem` under
Settings → Privacy & Security → Certificates, or add a one-time exception.

Notes:

- Regenerate per machine, never commit: `.env` and `.certs/` are gitignored.
  If the cert expires or `fetch` fails with
  `UNABLE_TO_VERIFY_LEAF_SIGNATURE`, re-run steps 1–3.
- Plain HTTP needs none of this: it is the default lane above
  (`--launch-profile http` + `bun run dev:http`), and `.env.example`'s
  commented http-only block is its `.env` equivalent.
- `launchSettings.json` keeps two profiles: `http` (first, so bare `dotnet run`
  picks it) serves `http://localhost:5207`, and `https` serves
  `https://localhost:5208`. Each profile serves its own URL; the https lane
  needs the explicit `--launch-profile https` from step 4.

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
│   │   │   ├── api.ts        # timed fetch helpers (incl. listRuns/getRun/listRunPostings/startRun/cancelRun, users/roles/quotas/auth/db); UI maps stable codes, never prose
│   │   │   ├── router.ts     # pure hash router: #/overview #/health #/profiles #/sources #/sessions #/runs #/topology #/database #/users #/roles #/quotas (codes 01–11)
│   │   │   └── telemetry.ts  # samples, stats, event log (pure, unit-tested)
│   │   ├── components/   # module frame, sparkline, tac-map, scramble, legend,
│   │   │                 # compass, ruler-tabs (ArtCraft rail × radar blip)
│   │                 # (tac-map: god's-eye battlespace per iart-ai/svg-animation:
│   │                 #  sweep, tracers, blips — SMIL, motion-gated)
│   │   ├── pages/        # overview, health, profiles, sources (+ Meter), sessions, runs, topology, database, users, roles, quotas
│   │   ├── fonts/        # IBM Plex Mono woff2, self-hosted (bundled as data URIs)
│   │   └── styles/       # design system: tokens, grid, modules, no-overflow rules
│   └── tests/
│       ├── server/       # config, proxy (incl. live stub upstream), routes
│       └── client/       # router, telemetry, api client units, runs page, deck interaction
└── dist/                 # build output (gitignored)
```

## Conventions

- Same-origin only: the browser talks to the BFF; the BFF talks to
  `127.0.0.1`. CORS is never opened.
- Failures render the stable `code` (`PROFILE_NOT_FOUND`,
  `VALIDATION_FAILED`, `DOTNET_UNREACHABLE`, …), never prose parsing.
- One page per feature, hash-routed (`#/overview`, `#/health`, `#/profiles`, `#/sources`, `#/sessions`,
  `#/runs`, `#/topology`, `#/database`, `#/users`, `#/roles`, `#/quotas`): deep links survive refresh with zero server config.
- Runs page (`#/runs`, code 06): the start form offers enabled profiles and
  only the sessions of the chosen profile's source (sessions that cannot
  launch are disabled); a history table; the run detail (stage track,
  page/posting/detail meters via `Meter`'s `progress` prop — reaching a cap
  reads as done, not as a warning — and Cancel); and the run's postings
  table. `RUN_STARTED` and the run's finish go to the event log.
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
- Render radar: with `?renders=1`, every component logs `[render] Name #N` +
  `[mount]`/`[unmount]` via `useRenderLog`, and every API call logs
  `[fetch] GET path`. Off by default: logging every render cost ~35 console
  calls a second on an idle deck, which an open devtools console turns into
  real CPU and memory. Render counts are always kept; read them with
  `__deckRenders()` in the console (tests use `renderCount`).
- Runtime budget, pinned by `tests/client/perf.test.tsx`: the UTC clock owns
  its tick, so an idle deck commits one `<span>` a second, not the page; the
  health auto-probe runs every 10 s only while the tab is visible and catches
  up once on return; a list response that arrives after a newer request for
  the same list is dropped (latest wins); a non-JSON 2xx body becomes a
  `BAD_RESPONSE` problem instead of a throw that would stall the probe.
  The selected run is polled every 2 s (`RUN_POLL_MS`) through a
  `setTimeout` chain, so the next request starts only after the last answer;
  polls are skipped while the tab is hidden and stop once the run is
  terminal.
- Motion is a system, not decoration: one spring token, staggered deck
  entrances, draw-in charts, breathing live-dots, press physics on controls
  (per `emilkowalski/animate` + `mengto/animation-systems`). Transform and
  opacity only; `prefers-reduced-motion` disables it all. Continuous motion
  uses CSS keyframes, never SMIL (SMIL restyles the SVG every frame on the
  main thread), and an infinitely animated shape gets its own element so its
  transform runs on the compositor (the compass needle overlays the tape).
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
| ≤720 | 6-col grid, full-width rows, tighter padding; the ruler rail shows stop codes only (labels visually hidden, kept for link names) | fluid minimums |
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

- **Ports are `3000` (BFF) + `5207` (http API) + `5208` (https API).** The BFF default follows the https profile (`https://localhost:5208`); `dev:http` follows the http one.
- **Auth ships; TanStack, shadcn, and SSE do not yet.** The BFF holds the session server-side (HttpOnly cookie, CSRF double-submit + origin check, session bearer upstream; login at `POST /bff/session`, first boot at `POST /bff/session/setup`, sign-out at `DELETE /bff/session`). A handful of fetches do not earn a query client (YAGNI). The runs vertical shipped without SSE: it uses the spec's documented fallback, polling `GET /api/v1/runs/{id}` every 2 s (the API has no events endpoint). TanStack, the `shadcn/ui` copy-in, and the SSE event proxy wait for the jobs vertical or a real need.
- **No `jest-dom`.** Its matchers have no `bun:test` type augmentation;
  the suite asserts on `textContent`. Re-add with proper setup if matchers
  earn their keep.
- **`@parcel/watcher` postinstall stays blocked.** One-shot Tailwind builds
  work without it; `dev:css --watch` will need
  `bun pm trust @parcel/watcher` with a PR reason when the loop demands it.
- **Favicon lives in `src/client/`, not `public/`.** Bun HTML imports
  resolve assets relative to the importing HTML; there is no `public/`
  convention. The `public/` folder was removed.

## Follow-ups

SSE run-event proxy (once the API has
`/runs/{id}/events`), run resume/artifacts views, TanStack
Query/Router/Table, shadcn copy-in, `api:types` codegen into
`src/shared/contracts.ts`, `openapi-typescript` wiring, production
single-binary (`bun build --compile`) smoke.
