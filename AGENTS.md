## Workspace boundary (strict)

- Work only inside the current repository folder. Never create, modify, or delete files outside it.
- Never install, update, or remove software, tools, libraries, or packages outside the repo folder without explicit permission. This covers brew, wget, curl, language package managers, global tool installs, browser downloads, and system configs.
- Standard restores required by an explicitly ordered step (`dotnet restore`, `bun install`) may populate their default caches. Anything beyond that needs explicit permission first.
- When in doubt, stop and ask; state the exact command and why. A blocked step beats an unauthorized change.
- Default to readonly commands (`read`, `grep`, `glob`, `git status`, `git log`, `git diff`). Destructive means data loss or force overwrite (`rm -rf`, `git reset --hard`, `git push --force`). System-modifying means persistent change outside the edit tools (installs, uninstalls, `chmod`, writes outside the repo).
- Avoid installing browsers, SDKs, or tools globally or into user caches. If a tool can only fetch outside the repo, ask first or set an in-repo cache path (e.g. `PLAYWRIGHT_BROWSERS_PATH=./ms-playwright`).
- Never write logs, temp files, caches, browser binaries, or generated artifacts outside the repo folder. Redirect command output to `/dev/null` or an in-repo path; never `/tmp` or the home directory.
- Long-running servers and background jobs must not leave log files or pid files outside the repo; use `/dev/null` for their output.
- Other than the restore caches allowed above, if any step would touch a path outside the repo, stop and ask before running it.

## Architecture

- No CQRS command/query buses, no MediatR/mediator, no AutoMapper, and no generic repositories.
- Use explicit constructor-injected use-case handlers; call them directly from CLI commands and API endpoints.
- Use the project patterns: `Result<T>` for expected failures, validated Options at the composition root, Specifications for reusable query criteria, and RFC 9457 Problem Details at the API edge.
- Read [docs/14-application-patterns.md](docs/14-application-patterns.md) before adding an application abstraction or error contract.
- Runtime rules: Serilog message templates and redaction, global exception handling, `IHttpClientFactory` + Polly for HTTP, `CancellationToken` on every I/O boundary, `using`/`await using`, bounded queues, disk checks. See [docs/15-runtime-hardening.md](docs/15-runtime-hardening.md).
- Library rule: use each package’s documented, recommended configuration. Do not remove or restrict a library feature because of AOT/trimming. Serilog, DataAnnotations, ClosedXML, Scalar, EF Core, and Playwright are used as designed. See [docs/16-library-usage-policy.md](docs/16-library-usage-policy.md).

## Local development (backend + frontend)

Run the .NET API first, then the Bun BFF. The BFF proxies `/api/*` to the API. Local dev is plain HTTP; the HTTPS lane exists only for testing TLS behavior.

**Backend (.NET 11, ASP.NET Core)**
- Project: `src/backend/JobScrape.Api/JobScrape.Api.csproj` (solution: `src/backend/JobScrape.slnx`).
- SDK: `11.0.100` pinned in `src/backend/global.json`, which also selects the Microsoft.Testing.Platform runner. `dotnet` resolves `global.json` by walking up from the working directory, so it is only picked up inside `src/backend`; the repo-root command below bypasses the pin. Run `cd src/backend && dotnet run --project JobScrape.Api/JobScrape.Api.csproj --launch-profile http` to honor it, or confirm `dotnet --version` is 11.0.x when running from the repo root.
- From repo root: `dotnet run --project src/backend/JobScrape.Api/JobScrape.Api.csproj --launch-profile http`.
- Profiles (`src/backend/JobScrape.Api/Properties/launchSettings.json`): `http` → `http://localhost:5207` (default, no cert); `https` → `https://localhost:5208`.
- API docs (Development only): `http://localhost:5207/scalar` and `http://localhost:5207/openapi/v1.json`.
- Dev data: SQLite at `src/backend/JobScrape.Api/artifacts/data/jobscrape.db`; migrations apply on start in Development (elsewhere they are an operator step). To reset, stop the API, then `rm src/backend/JobScrape.Api/artifacts/data/jobscrape.db*` (the `*` clears the WAL sidecars too).
- Health: `curl http://localhost:5207/api/v1/health` → `{"status":"healthy","timeUtc":"..."}`.
- Logs: Serilog compact JSON to stdout plus rolling files in `src/backend/JobScrape.Api/artifacts/logs/`. Two expected `CollectionWithoutComparer` warnings (`ScrapeRun.Companies`, `ScrapeRunProgress.SeenExternalIds`) appear each boot; both lists are replaced whole, never mutated, so they are accepted. dotnet's own `Building...` and crash output stay plain text.

**Frontend (Bun 1.4.4, React 19.3.1, Tailwind 4.3.5)**
- Dir: `src/frontend`. Install once: `bun install`.
- Default (http) lane, no cert, no `.env`: `cd src/frontend && bun run dev:http` → BFF on `http://127.0.0.1:3000`, pinned to `http://127.0.0.1:5207`. The script blanks `BFF_TLS_*` itself, so a leftover `.env` from the https lane cannot break it; only `bun run dev` needs the https block.
- Styles: `src/client/styles/globals.css` is linked directly and hot-reloads with the dev server; no watcher needed. `bun run dev:css`/`build:css` write a gitignored `generated.css` that nothing currently links.
- HTTPS lane (optional, one-time cert setup): backend on `--launch-profile https`, then `cp .env.example .env` in `src/frontend` (its active block sets `BFF_TLS_*`) and `bun run dev` → `https://127.0.0.1:3000` plus the Tailwind watcher. Export steps: `src/frontend/README.md` § HTTPS dev loop.
- Health: `curl http://127.0.0.1:3000/healthz` → `{"status":"ok","upstream":"..."}` (BFF only, no CA file needed); `curl http://127.0.0.1:3000/api/v1/health` → `{"status":"healthy","timeUtc":"..."}` (both hops).
- App: `http://127.0.0.1:3000/#/overview`. Sign in: the deck is auth-gated. First run uses "First login / setup password" with the seeded secret `admin@example.com` / `Setup-Test-12!!` (`JobScrape:AdminSeed` in `appsettings.json`), which sets your password; later runs sign in with that email + password. Development-only default; production must override `JobScrape:AdminSeed` (or remove the section).

**Debugging**
- The BFF boots without the API, but every proxied `/api/*` call answers 502 `DOTNET_UNREACHABLE` until the API is up (the blocked auth routes below answer 404), so start the API first.
- `/api/v1/auth/login` and `/api/v1/auth/setup-password` are BFF-owned and answer 404 to direct calls by design; the deck uses `/bff/session` and `/bff/session/setup`.
- Kill stale processes: `lsof -ti:5207 | xargs kill` and `lsof -ti:3000 | xargs kill` (port-scoped; `pkill -f JobScrape.Api` also hits E2E backends on 5399 and other checkouts).
- Port map: API 5207 (http) / 5208 (https); BFF 3000. E2E (`bun run test:e2e` from `src/frontend`) boots its own API/BFF on 5399/3399 with a temp DB, so keep those free. Playwright installs write outside the repo (ask first); keep them in-repo with `PLAYWRIGHT_BROWSERS_PATH=./ms-playwright bunx playwright install chromium` (BFF/E2E side). API-side Playwright sources need a build first, then `src/backend/JobScrape.Api/bin/Debug/net11.0/.playwright/node/darwin-arm64/node src/backend/JobScrape.Api/bin/Debug/net11.0/.playwright/package/cli.js install chromium` (or `pwsh .../playwright.ps1 install chromium` with PowerShell 7; docs/04-solution-and-tooling.md §7.3).

## Commits

Follow [Conventional Commits v1.0.0](https://www.conventionalcommits.org/en/v1.0.0/#specification); the project policy is [docs/13-conventional-commits.md](docs/13-conventional-commits.md).

- Format: `<type>(<scope>)!: <description>`; one logical change per commit.
- Allowed types: `build`, `chore`, `ci`, `docs`, `feat`, `fix`, `perf`, `refactor`, `revert`, `style`, `test`.
- Imperative, lowercase description with no trailing period; target 72 characters, hard maximum 100.
- Put the why in the body after one blank line; reference issues in footers (`Refs: #42`).
- Enable the local validator once per clone: `git config core.hooksPath .githooks`.
- Never bypass the hook to avoid fixing a message; fix the message or the hook.
- PR titles follow the same format because squash merges use them as the commit header.

## UI design

- When designing or developing UI in `src/frontend/`, consult the ui-skills catalog first: `list_skills` filtered by topic/intent, then `get_skill` for the smallest useful set. Start with the `ibelick/ui-skills-root` router skill when unsure which applies.
- Project-local UI skill pack also installed (`.opencode/skills/ui-ux-pro-max/`, plus `design`, `design-system`, `ui-styling`, `slides`, `banner-design`, `brand`): use `/ui-ux-pro-max` as orchestrator, or the sibling directly. Data lookups run via `python3 .opencode/skills/<skill>/scripts/search.py` (stdlib only). Image-generation steps in `banner-design`/`design` need the author's sibling skills (`frontend-design`, `ai-artist`) + API keys and are skipped without them. See [docs/18-harness-packs.md](docs/18-harness-packs.md).
- Design baseline is 1920×1080 (16:9, ~141 PPI); fluid type/layout bottoms out below and ceilings at the baseline. See `src/frontend/README.md` § Design baseline.
- Reference: [UI Skills](https://www.ui-skills.com/skills) · [MCP docs](https://www.ui-skills.com/mcp/docs).

## Agent workflow (pstack)

- pstack is installed project-locally (`.opencode/skills/`, `.opencode/agents/`, `.opencode/commands/`). See [docs/17-pstack-opencode.md](docs/17-pstack-opencode.md).
- For any non-trivial task, start with `/poteto-mode`: it matches the request to a playbook and routes to the other skills as steps need them.
- Role-to-model mapping lives in `.opencode/pstack-models.md` (auto-loaded). A step needing a Cursor-only feature (Bugbot, Automations, cloud agents) is skipped with a one-line `Skipped in OpenCode:` note per `.opencode/docs/harness/opencode.md`.
- Choose the subagent model from `.opencode/pstack-models.md` by role; a role with no line runs on the parent chat model, and the brief says so explicitly with `inherit-parent` — no brief leaves it blank.
- Every subagent reference — names, descriptions, briefs, status tables, review verdicts — carries provider + model + thinking-effort variant (`provider/model#variant`; bare ID only when the catalog lists no variants).
- Execution defaults (binding): plan spec-first with `writing-plans` (investigation → spec → caveats → task list with one test per task; or the lean plan gate in `working-lean-with-poteto`); then execute with SDD — `subagent-driven-development` — when tasks are independent (`executing-plans` inline when they are coupled); implement with TDD — `test-driven-development` — inside each task brief, RED-first per behavior change; state the role and its model — or `inherit-parent` — in every brief; read-only reviews and next-task setup may overlap, but implementers run one at a time per plan. Full ceremony only where it earns its place; trivial edits go direct.
- Pre-dispatch check (binding — answer before every execution; record in the brief, or in reply for direct work): role and model from `.opencode/pstack-models.md`, or explicit `inherit-parent`. Never dispatch on an unnamed default.
- SDD task with brief, or trivial edit going direct — and if direct, one-line why. No third path: another planning flow (`superpowers` brainstorming/plans, ad-hoc planning) does not substitute for `writing-plans` plus a task brief.
- For dispatched work: name what runs in parallel with this step and what waits on it.
- Launch independent workers in one turn. When you wait on all of them, leave `background` off so they return together; use `background: true` only when you have other work to do meanwhile. This overrides skill background defaults.
- Any of the three missing — role/model, execution route, parallel plan — stop and resolve it before executing.
- `deslop` ships from `cursor-team-kit` (`.opencode/skills/deslop/`, `/deslop` command).

## ECC (secondary workflow pack)

- ECC commands live in `.opencode/commands/` (`/plan`, `/code-review`, `/security`, `/build-fix`, `/e2e`, `/refactor-clean`, `/orchestrate`, `/learn`, `/checkpoint`, `/verify`, `/eval`, `/update-docs`, `/test-coverage`, `/harness-audit`, `/quality-gate`, `/model-route`, `/loop-start`, `/loop-status`, `/skill-create`) backed by 8 agents in `.opencode/opencode.json` (`planner`, `code-reviewer`, `security-reviewer`, `tdd-guide`, `build-error-resolver`, `e2e-runner`, `doc-updater`, `refactor-cleaner`) and consolidated rules in `.opencode/instructions/INSTRUCTIONS.md` (auto-loaded). No plugin hooks/tools (not built); instinct system, language-specific agents/reviewers (go/rust/java/kotlin/cpp/php/ruby), `setup-pm`, `security-scan` (needs AgentShield), and ECC `/tdd` (name owned by pstack) are skipped. See [docs/18-harness-packs.md](docs/18-harness-packs.md).
- `/tdd` is the pstack skill; ECC's TDD agent is reachable via `/test-coverage` and the `tdd-guide` subagent.

## OpenCode config scope

- OpenCode configuration for this repo is project-local only: `.opencode/` and this file. There is no global `~/.config/opencode/` config (no global skills, agents, commands, plugins, or instructions), so sessions here see only what the repo ships.

## Response formatting

- Responses stay terse (answer first, no ceremony); code, comments, commits, PRs, and docs stay normal prose.
- These rules do not override a skill's or agent's own required response format. Where they conflict, the skill wins.
- unslop rule 18 still applies: an emoji that helps scanning is not decorative, so keep one per heading or list item and drop the rest.

### Tables

- When a reply carries comparable numbers, options, or findings a reader would line up by hand, use a table with the columns the comparison needs.
- Number the rows of peer-item tables with an index column so each item stays referenceable in the surrounding prose. Key-value tables need no index.
- When the content is a single fact, a narrative, or a judgment with no peers, keep prose. Never force content into a table to decorate it.

### Emoji

- Emoji are for chat replies; artifacts stay plain. Emojis are optional: at most one per heading or list item, and only where it helps scanning. Reuse the same emoji for items of the same kind.
- Omit the emoji where a word is clearer. Keep emoji out of code, commands, paths, comments, and any commit, PR, issue, or doc a reply writes or quotes.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

When the user types `/graphify`, use the installed graphify skill or instructions before doing anything else.

Rules:
- When graphify-out/graph.json exists, run `graphify query "<question>"` before your first grep, search or source-file read for a codebase question, and put this rule in every subagent prompt that explores code. The graph also indexes the full `.opencode/` corpus (every skill, playbook, reference, command, agent, and the harness docs), so skill discovery ("which skill/command achieves X") goes through `graphify query` too, then read the located `SKILL.md` in full before invoking it. Read raw files only for lines the graph or an earlier read already located. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- Dirty graphify-out/ files are expected after hooks or incremental updates; dirty graph files are not a reason to skip graphify. Only skip graphify if the task is about stale or incorrect graph output, or the user explicitly says not to use it.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
- A committed graphify-out/graph.json is rewritten whole on every build, so a large diff to it in a pull request is expected. Do not flag it as churn or suggest skipping the graph rebuild or its hooks.
