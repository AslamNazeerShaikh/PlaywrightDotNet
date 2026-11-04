# pstack on Claude Code

pstack (poteto's agent workflow plugin, [cursor/plugins](https://github.com/cursor/plugins/tree/main/pstack))
is installed project-locally for Claude Code. It turns the agent into a rigorous
engineering team: match the task to a playbook, run the steps, verify against
the real artifact. The goal is less, higher-quality code. The OpenCode install
is separate and unchanged; see [17-pstack-opencode.md](17-pstack-opencode.md).

## Use

1. Optional: run `/setup-pstack` to pick a reasoning budget and per-role
   models. It rewrites `.claude/rules/pstack-models.md`, which Claude Code
   loads into every session. The committed defaults are upstream's, mapped by
   the adapter: opus roles run `opus xhigh`, grok roles run on the parent
   model (`inherit-parent`).
2. Use `/poteto-mode` at the start of any task that needs rigor. It matches
   your request to one of 23 playbooks, opens a todo list from the playbook's
   steps, and routes to the other skills as the steps need them.

```text
/poteto-mode this pr has a subtle bug where the scroll drifts every 750ms even when idle. repro first, then fix and verify.
```

Stuck, or unsure which skill fits? Ask `/poteto-help`. Skills open in a new
session; restart Claude Code after a re-sync.

## Why vendored, not the plugin

Upstream also publishes pstack as a Claude Code plugin
(`/plugin marketplace add cursor/plugins`, `/plugin install pstack@cursor-plugins`).
That install lives per machine, stays on whatever version each person
installed (third-party auto-update is off), and keeps its model sheet in
`~/.claude/rules/`. Vendoring the same generated tree pins one reviewed
version in git, scopes the model sheet to this repo, and drops the `pstack:`
prefix. Do not install the plugin as well: both copies would load.

## Layout

| Upstream (`harness/pstack/claude-code/`) | This repo |
| --- | --- |
| `skills/` (58 skills) | `.claude/skills/` (verbatim; `../../docs/` links resolve) |
| `agents/` (`poteto-agent`, `comment-sicko`) | `.claude/agents/` (verbatim, plus a repo harness note on `poteto-agent`) |
| `docs/harness/claude-code.md`, `docs/guide/` | `.claude/docs/` (verbatim) |
| `README.md`, `LICENSE` | `.claude/README.md`, `.claude/pstack-LICENSE` |
| `cursor-team-kit/skills/deslop` | `.claude/skills/deslop/` (verbatim) |
| `~/.claude/rules/pstack-models.md` | `.claude/rules/pstack-models.md` |
| Plugin-specific adapter rows (`pstack:` prefix, `~/.claude/plugins/`) | Overridden in the root `CLAUDE.md` |

The generated tree is upstream's raw `pstack/skills` plus one line per skill
pointing at the adapter. Claude Code mappings live only in the adapter and in
the `CLAUDE.md` overrides, so a re-sync is a re-copy plus diff.
`.claude/settings.local.json` is gitignored, and `.graphifyignore` skips
`.claude/` because the graph already indexes the same corpus under `.opencode/`.

## Re-syncing with upstream

```sh
git clone --depth 1 --filter=blob:none --sparse https://github.com/cursor/plugins.git /tmp/pstack-up
cd /tmp/pstack-up && git sparse-checkout set harness/pstack/claude-code cursor-team-kit/skills/deslop
P=harness/pstack/claude-code
diff -rq $P/skills <repo>/.claude/skills | grep -v "Only in <repo>/.claude/skills: deslop"
diff -rq $P/docs <repo>/.claude/docs
diff -q cursor-team-kit/skills/deslop/SKILL.md <repo>/.claude/skills/deslop/SKILL.md
```

Copy changed files over (never rename: `name` must equal the directory). If
`agents/poteto-agent.md` changed, re-copy it and re-append the harness note
from the current file's last paragraph. If the adapter gained rows that assume
the plugin install, add the repo override to `CLAUDE.md`.

## Validation

After any change: every `.claude/skills/*/SKILL.md` has `name` equal to its
directory and a `description` of 1 to 1024 characters; every relative link in
`.claude/skills/` and `.claude/agents/` resolves; `.claude/agents/*.md` have
`name` and `description`; and a new session lists `/poteto-mode` and the
`poteto-agent` and `comment-sicko` agents.
