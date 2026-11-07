# Additional harness packs: ui-ux-pro-max, ECC

Two packs installed alongside pstack ([docs/17](17-pstack-opencode.md)).
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
    the rest bind to an ECC subagent through their own frontmatter
    (`agent: <name>`, `subagent: true`). Commands are defined only as
    Markdown: OpenCode V2 lets a Markdown command replace a same-named JSON
    entry, so JSON `commands` entries would be dead config.
  - 8 agents under `agents` in `.opencode/opencode.json` (V2 shape:
    `system: "{file:prompts/agents/<name>.txt}"` and an ordered
    `permissions` array using V2 actions `read`, `shell`, `edit`). Prompts
    live in `.opencode/prompts/agents/`; `{file:...}` resolves from
    `.opencode/`. The plugin-only `changed-files` custom tool was dropped
    (no plugin installed).
  - `.opencode/instructions/INSTRUCTIONS.md`, wired via `instructions`
    (project-root-relative paths; verified loaded).
- Skipped, with reasons:
  - `/tdd` name collision: owned by the pstack skill. ECC's `tdd-guide`
    agent stays reachable via `/test-coverage`.
  - Plugin (`plugins/`, `tools/`, hooks, custom tools): written for the
    V1 plugin API (`@opencode-ai/plugin`), which OpenCode V2 does not run.
    It needs a port to the V2 `@opencode/plugin` API before it can load.
    Until then, hook-gated behavior is absent and agents degrade to
    prompting.
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

## Caveman (removed)

Caveman was a machine-global OpenCode install (`~/.config/opencode/`). It was
removed with the rest of the global OpenCode config on 2026-11-07 so this
repo's OpenCode setup is project-local only. The repo never carried caveman
files.

## Validation (both)

- `python3` frontmatter sweep: every `.opencode/skills/*/SKILL.md` has
  `name` == directory (lowercase hyphenated) and `description` 1–1024 chars.
- Every `.opencode/commands/*.md` resolves: pstack/ui-ux-pro-max loaders
  name an installed skill; each ECC command's frontmatter `agent` names a
  registered agent; every agent's `{file:prompts/agents/*.txt}` exists.
- `python3 -c "import json; json.load(open('.opencode/opencode.json'))"`,
  `opencode debug config` loads with no warnings.
- `python3 .opencode/skills/ui-ux-pro-max/scripts/search.py --help` runs
  (stdlib only).
