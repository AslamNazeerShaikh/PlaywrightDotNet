# Additional harness packs: ui-ux-pro-max, ECC, caveman

Three packs installed alongside pstack ([docs/17](17-pstack-opencode.md)).
Conventions match: official installer first, project-local where the tool
allows it, verbatim copies, deltas recorded here.

## ui-ux-pro-max (project-local skills)

- Source: <https://github.com/nextlevelbuilder/ui-ux-pro-max-skill>, CLI
  `ui-ux-pro-max-cli` (ran `init --ai opencode`, template-generated).
- Installed: 7 skills in `.opencode/skills/`
  (`ui-ux-pro-max`, `design`, `design-system`, `ui-styling`, `slides`,
  `banner-design`, `brand`, ~4.6 MB data + stdlib-only `search.py`) and 7
  matching slash commands in `.opencode/commands/`.
- No agents ship with the pack; none added.
- Gaps (upstream, same on Claude): `banner-design`/`design` image steps
  reference the author's sibling skills (`frontend-design`, `ai-artist`,
  `ai-multimodal`) with hardcoded `.claude/skills/` paths and need image API
  keys. Core HTML/CSS + `search.py` paths work as-is; image steps are skipped
  without those skills/keys.
- Update: `npx ui-ux-pro-max-cli@latest init --ai opencode --force`
  (re-run; pass `--token` or `GITHUB_TOKEN` if rate-limited).

## ECC (project-local commands, agents, instructions)

- Source: <https://github.com/affaan-m/ECC> (`ecc-universal@2.2.3` era),
  manual port of its bundled `.opencode/` assets (their `install.sh
  --target opencode` targets the global user config; this repo keeps the
  project-local "copy the assets and wire the entries" path from their docs).
- Installed:
  - 20 commands in `.opencode/commands/` (see AGENTS.md list). `learn`,
    `checkpoint`, `verify`, `eval`, `quality-gate`, `harness-audit`,
    `model-route`, `loop-start`, `loop-status` run on the current agent;
    the rest bind to an ECC subagent via `command` entries in
    `.opencode/opencode.json` (`subtask: true`).
  - 8 agents in `.opencode/opencode.json` with prompts in
    `.opencode/prompts/agents/`. Their deprecated `tools` blocks were
    converted to `permission` (`write`/`edit` to `edit`, `read` to `read`,
    `bash` to `bash`); the plugin-only `changed-files` custom tool was
    dropped (no plugin installed).
  - `.opencode/instructions/INSTRUCTIONS.md`, wired via `instructions`
    (project-root-relative paths; verified loaded).
- Skipped, with reasons:
  - `/tdd` name collision: owned by the pstack skill. ECC's `tdd-guide`
    agent stays reachable via `/test-coverage`.
  - Plugin (`plugins/`, `tools/`, hooks, custom tools): needs
    `npm install` + `tsc` build inside `.opencode/` (`@opencode-ai/plugin`
    peer). Follow-up: build it, add `"plugin": ["./plugins"]` — or
    `npm install ecc-universal` + `"plugin": ["ecc-universal"]` per their
    npm path. Until then, hook-gated behavior is absent and agents degrade
    to prompting.
  - Instinct system (`instinct-*`, `evolve`, `promote`, `projects`):
    requires the plugin runtime (`continuous-learning-v2`).
  - Language reviewers/builders for go/rust/java/kotlin/cpp/php/ruby,
    `database-reviewer`, `docs-lookup` (needs Context7 MCP, not installed),
    `loop-operator`/`harness-optimizer` (unreferenced), `setup-pm`
    (node-oriented), `security-scan` (needs AgentShield scanner).
  - Their `permission: {mcp_*: ask}` default and `skills.paths` were not
    merged (would change existing MCP approvals and point at a missing dir).
- Their instructions file lists `/tdd` as "Enforce TDD workflow" (ECC's
  meaning); here `/tdd` runs the pstack skill. Behavior is close enough to
  leave their file verbatim; this note is the record.
- Update: re-copy changed files from a fresh ECC clone (paths mirror
  1:1: `commands/`, `prompts/agents/`, `instructions/`), re-run the
  validation below.

## Caveman (global install, repo pointer only)

- Source: <https://github.com/JuliusBrussee/caveman>. Installed with
  `node installer/install.js --only opencode` (no `--with-init`, so the repo
  was untouched): plugin + server in `~/.config/opencode/plugins/caveman/`
  (confirmed in `opencode plugin list`), 9 skills + 8 commands + 3
  `cavecrew-*` agents in global config, `plugin` entry patched into global
  `opencode.jsonc` (backup at `/tmp` is ephemeral — reinstall replays it),
  Tier-3 ruleset appended to global `AGENTS.md`.
- Scope: only `openai`/`anthropic`/`opencode-go` provider traffic is routed
  through caveman (this repo's sessions qualify via `opencode-go`). Other
  providers bypass it; `caveman doctor opencode` warns when the model uses
  one.
- The repo carries no caveman files by design (machine-global tool). The
  AGENTS.md section above is the standing instruction for sessions here.
- Update/uninstall: `npx -y github:JuliusBrussee/caveman -- --only opencode`
  (re-run), `--uninstall` to remove.

## Validation (all three)

- `python3` frontmatter sweep: every `.opencode/skills/*/SKILL.md` has
  `name` == directory (lowercase hyphenated) and `description` 1–1024 chars.
- Every `.opencode/commands/*.md` resolves: pstack/ui-ux-pro-max loaders
  name an installed skill; ECC `command` entries reference existing
  `commands/*.md` + registered agents; agent `prompt` files exist.
- `python3 -c "import json; json.load(open('.opencode/opencode.json'))"`,
  `opencode debug config` loads, `opencode plugin list` shows caveman.
- `python3 .opencode/skills/ui-ux-pro-max/scripts/search.py --help` runs
  (stdlib only).
