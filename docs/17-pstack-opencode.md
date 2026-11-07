# pstack on OpenCode

pstack (poteto's agent workflow plugin, [cursor/plugins](https://github.com/cursor/plugins/tree/main/pstack))
is installed project-locally for OpenCode V2 (2.2.6) from the
[backnotprop/pstack](https://github.com/backnotprop/pstack) mirror. It turns
the agent into a rigorous engineering team: match the task to a playbook, run
the steps, verify against the real artifact. The goal is less, higher-quality
code.

OpenCode configuration for this repo lives only in `.opencode/` and
`AGENTS.md`. There is no global `~/.config/opencode/` config: no global
skills, agents, commands, plugins, or instructions leak into sessions here.

## Use

1. Optional: run `/setup-pstack` to pick a reasoning budget and per-role
   models. It rewrites `.opencode/pstack-models.md`, auto-loaded every session
   via `instructions` in `.opencode/opencode.json`. A role value is
   `provider/model#variant`, where the variant carries the effort.
2. Use `/poteto-mode` at the start of any task that needs rigor. It matches
   your request to one of 23 playbooks, opens a todo list from the playbook's
   steps, and routes to the other skills as the steps need them.

```text
/poteto-mode this pr has a subtle bug where the scroll drifts every 750ms even when idle. repro first, then fix and verify.
```

Stuck, or unsure which skill fits? Ask `/poteto-help`.

## Layout

| Mirror (`backnotprop/pstack`) | This repo |
| --- | --- |
| `skills/` (58 skills) | `.opencode/skills/`. Mirror copies, with OpenCode edits in `poteto-mode`, `setup-pstack`, and `no-comments`, and V2 corrections to the mirror's generic OpenCode lines (see Re-syncing) |
| `agents/` (`poteto-agent`, `comment-sicko`) | `.opencode/agents/`, registered as V2 subagents (`mode: subagent`). `poteto-agent` carries an OpenCode harness note |
| Slash invocation (`/poteto-mode`) | `.opencode/commands/`, one loader per non-`principle-*` skill, plus `/deslop` |
| `docs/guide/`, `README.md` | `.opencode/docs/guide/`, `.opencode/README.md` (kept so skill-relative links resolve) |
| `docs/harness/claude-code.md` | Kept for reference; OpenCode sessions use `.opencode/docs/harness/opencode.md` |
| `~/.cursor/rules/pstack-models.mdc` | `.opencode/pstack-models.md` |
| `cursor-team-kit/skills/deslop` (upstream) | `.opencode/skills/deslop/` (verbatim) |
| `automations/benny` | Not copied. It runs on Cursor Automations (Cursor-only) |

OpenCode-specific mappings (tools, subagents, models, paths, skipped
Cursor-only steps) live in `.opencode/docs/harness/opencode.md`, which
sessions read once per task. `control-cli` and `control-ui` are not
installed; those steps are skipped with a one-line note and CLI/UI work is
verified with `dotnet test` and Playwright.

## OpenCode V2 facts this setup relies on

- Subagents spawn with the `subagent` tool (`agent`, `description`, `prompt`,
  optional `model`, `background`, `sessionID`). V1's `task` tool and
  `subagent_type` do not exist. Nesting depth is 1, so fan-out runs from the
  parent session.
- `.opencode/agents/*.md` with `mode: subagent` registers the agent;
  `opencode debug agents` lists `poteto-agent` and `comment-sicko`.
- Models are `provider/model#variant` for agents, commands, and subagent
  calls. Variant names differ per model; `opencode api GET /api/model` lists
  them. The root `model` drops any variant.
- The `skill` tool takes `id`. Most pstack skills set
  `disable-model-invocation: true`, which hides them from the advertised list,
  but the tool still loads them by ID.
- `experimental.todo: true` enables `todowrite` (off by default in V2).
- A Markdown command replaces a same-named JSON `commands` entry, so commands
  are defined only as `.opencode/commands/*.md`.
- V2 reads `AGENTS.md` only, never `CLAUDE.md`. It also discovers project
  `.claude/skills/`; every skill there has a same-named copy in
  `.opencode/skills/`, which takes precedence.

## Re-syncing with the mirror

Check the mirror's `SYNC-LOG.md`, then diff and copy changed files:

```sh
git clone --depth 1 https://github.com/backnotprop/pstack.git /tmp/pstack-bn
diff -rq /tmp/pstack-bn/skills <repo>/.opencode/skills | grep -v "Only in <repo>"
```

Copy changed files over (never rename: `name` must equal the directory),
except the three OpenCode-edited skills (`poteto-mode`, `setup-pstack`,
`no-comments`), which are merged by hand. The mirror's harness-generic lines
still name V1 OpenCode tools, so after copying, rewrite them: `` `task` in
OpenCode (`subagent_type: general`) `` becomes `` `subagent` in OpenCode
(`agent: general`) ``, the settings file for OpenCode is
`.opencode/pstack-models.md`, the project skill folder is `.opencode/skills/`,
and OpenCode transcripts are `opencode session list` / `export`. Record the
sync in `.opencode/SYNC-LOG.md`.

## Validation

After any change: every `.opencode/skills/*/SKILL.md` has `name` equal to its
directory and a `description`; every `.opencode/commands/*.md` has frontmatter
with a `description`, and each loader names an installed skill;
`opencode debug config` loads with no warnings; `opencode debug agents` lists
`poteto-agent` and `comment-sicko` as subagents; and every `#variant` in
`.opencode/pstack-models.md` appears in that model's `variants`.
