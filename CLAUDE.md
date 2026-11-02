@AGENTS.md

## Claude Code

The project rules in `AGENTS.md` (architecture, commits, UI design, graphify) apply here too. Its pstack, ECC, and Caveman sections describe the OpenCode setup under `.opencode/`; in Claude Code, the pstack section below replaces them.

## Agent workflow (pstack)

- pstack is vendored project-locally from upstream's generated Claude Code tree (`.claude/skills/`, `.claude/agents/`, `.claude/docs/`). See [docs/19-pstack-claude-code.md](docs/19-pstack-claude-code.md).
- For any non-trivial task, start with `/poteto-mode`: it matches the request to a playbook and routes to the other skills as steps need them. Stuck, or unsure which skill fits? `/poteto-help`.
- Before following any pstack skill, read `.claude/docs/harness/claude-code.md` once per session. It maps Cursor names to Claude Code. This repo installs pstack as project files, not as the `pstack@cursor-plugins` plugin, so these overrides win over the adapter:
  - `<pstack>` is `.claude/` in this workspace. Run another pstack skill by reading `.claude/skills/<name>/SKILL.md`.
  - Commands have no prefix. Tell the user to type `/<name>` (for example `/poteto-mode`), never `/pstack:<name>`.
  - Subagents are `subagent_type: "poteto-agent"` and `subagent_type: "comment-sicko"`, never `pstack:poteto-agent` or `pstack:comment-sicko`. Pass each `poteto-agent` the absolute path of `.claude/skills/poteto-mode/SKILL.md`.
  - The role-to-model sheet is `.claude/rules/pstack-models.md` (auto-loaded every session). `/setup-pstack` writes that file, not `~/.claude/rules/pstack-models.md`.
- A step needing a Cursor-only feature (Bugbot, Automations, cloud agents) is skipped with a one-line `Skipped in Claude Code:` note.
- `deslop` ships from `cursor-team-kit` (`.claude/skills/deslop/`, `/deslop`). `control-cli` and `control-ui` are not installed; verify CLI and UI work with `dotnet test` and Playwright instead.
- Never edit `.opencode/` or `AGENTS.md` for Claude Code changes; Claude Code configuration lives in `.claude/` and this file.
