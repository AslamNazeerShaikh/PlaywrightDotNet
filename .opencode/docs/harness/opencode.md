# pstack in OpenCode

pstack skills use Cursor names. In OpenCode, read this file once per session, then map each Cursor name in a skill with the tables below. `<pstack>` means the folder that holds `skills/` and `docs/` — in this project, `.opencode/`. If a Cursor field has no row, drop it and say so. Never pass a field or value the tool schema rejects.

## Tools

| Cursor | OpenCode |
| --- | --- |
| `Task` (spawn a subagent) | `task` tool with `subagent_type` naming an agent (`poteto-agent`, `comment-sicko`, `general`, `explore`) |
| `AskQuestion` | `question` tool. If the session has no `question` tool, ask in plain text |
| Open a todolist | `todowrite` if the session offers it. Otherwise keep the list in your reply and update it at each phase |
| Run another pstack skill (`/name` or "the **name** skill") | `skill` tool with `name: "<name>"`. When you tell the user to type it, write `/<name>` (slash commands exist for every non-`principle-*` skill) |
| `Grep`, `Glob`, `Bash` | Same names in OpenCode (`grep`, `glob`, `bash`) |

## Subagent fields

| Cursor | OpenCode |
| --- | --- |
| `subagent_type: generalPurpose` | `general` (can edit) or `explore` (read-only). Pick per the skill's readonly flag |
| `poteto-agent`, `comment-sicko` | `poteto-agent`, `comment-sicko` (no prefix; agents live in `.opencode/agents/`) |
| `readonly: true` | `subagent_type: "explore"`. It keeps `bash`, so the brief also says "do not edit files", and you check `git status` afterward |
| `readonly: false` | `general`. It keeps the session's MCP servers |
| `run_in_background: true` | Omit. Subagents run async; drain them per the skill |
| `resume` / message / queue a follow-up on a running agent | Spawn a fresh `poteto-agent` with consolidated scope (original brief, later directives, prior report and branch), per poteto-mode's Subagents section. Resume only for state that is costly to move (local checkout, uncommitted changes, a still-running process) |
| `environment: "cloud"` | Omit. Run locally; name the branch or commit the worker checks out |
| `cloud_base_branch: <b>` | Tell the worker to check out `<b>` first |
| `file_attachments` | List the paths in the brief |

## Models

| Cursor | OpenCode |
| --- | --- |
| `claude-<family>-<version>-<effort>[-fast]`, e.g. `claude-opus-5-5-xhigh` | An OpenCode model ID `provider/model-id` (see `opencode models`). Effort/speed have no slug encoding; pick the model for the job and pass provider reasoning options (e.g. `reasoningEffort`) only if the model supports them |
| A `grok-*`, `gpt-*`, or other non-Claude slug | The session's equivalent code-model ID |
| `auto` or `inherit-parent` | Omit `model` so the subagent runs on the parent model |
| The `pstack-models.mdc` rule, `~/.cursor/rules/pstack-models.mdc` | `.opencode/pstack-models.md`, auto-loaded via `instructions` in `.opencode/opencode.json`. Read a role's line for its model; a missing file or line means `inherit-parent`. Never put role lines in `AGENTS.md` |
| A role value `<model> <effort>`, e.g. `opus high` | Just `<model>` (OpenCode ID). The skill's budget step maps to model choice, not a suffix |

Budget ladder for `/setup-pstack` re-runs: `unlimited` → strongest reasoning model available; `large` → default (matches skill defaults, everything on parent); `medium` → a cheaper/faster model for routine roles; `small` → cheapest for routine roles, strongest reserved for `hardest tasks` and judgment. Keep panel lists diverse (distinct models per entry); an `inherit-parent` entry still counts toward fan-out but never judges.

## setup-pstack

`.opencode/pstack-models.md` is auto-loaded into each session via `instructions`, so its role lines are already in context. If they are not, read the file. If the file or the role's line is missing, use `inherit-parent`. Follow the skill's steps with these changes:

- Detect. The detected models are the IDs listed by `opencode models` (plus the current session model). `inherit-parent` and `auto` are always valid even though they are not model IDs.
- Defaults. Start from this file's role lines (all `inherit-parent` until configured). There are no Cursor slugs to translate.
- Write. Overwrite `.opencode/pstack-models.md` wholesale: comment lines, the `# budget` line, one line per role. Never write role lines to `AGENTS.md` or `CLAUDE.md`.
- Verify. Every real model ID written must appear in `opencode models`. `inherit-parent` and `auto` always pass.

## Paths

| Cursor | OpenCode (this project) |
| --- | --- |
| `.cursor/skills/`, `~/.cursor/skills/` | `.opencode/skills/`, `~/.config/opencode/skills/` |
| `~/.cursor/plugins/`, `.cursor-plugin/plugin.json` | `.opencode/` (project-local install; no versioned plugin registry, so the "highest version" tiebreak becomes "prefer the project-local copy") |
| `.cursor/rules/*.mdc`, always-applied rules | `.opencode/opencode.json` `instructions` (auto-loaded, e.g. `pstack-models.md`), plus `AGENTS.md` |
| `https://github.com/cursor/plugins/blob/main/pstack/<path>` | `.opencode/<path>` when that file exists (`skills/`, `docs/guide/`, `README.md`). Read the local copy, never fetch it. Give the user the URL when they need a link |
| Transcripts in `~/.cursor/projects/<slug>/agent-transcripts/` | OpenCode sessions: `opencode session list` (current project, newest first) and `opencode session export`. Backed by the server database (see `opencode debug paths`). Read only this workspace's sessions |

## Cursor-only features

OpenCode has no Cursor Automations, Bugbot, Cursor cloud agents, Custom Modes, `/loop`, built-in `create-skill`, or `cursor-team-kit` (`deslop`, `control-cli`, `control-ui`). When a step needs one, put one line in your reply, `Skipped in OpenCode: <step>. <reason>.`, then go on to the next step. Do not stop, ask, or retry. When a later step needs what the skipped step made, such as a URL or a key, leave a placeholder and name it in the line.

| Cursor | OpenCode |
| --- | --- |
| Cursor Automations: routines, `update_state` routine calls, `api2.cursor.sh/automations` webhooks, routine wakes, and `SendToUser` secret requests | Skip with the line |
| Bugbot | Bugbot comments on a GitHub PR are ordinary review threads, so triage them with the forge command the step gives (`gh`). Skip a step that starts, waits for, or configures Bugbot when the PR has no Bugbot check or comment |
| Cursor cloud agents, `environment: "cloud"`, cloud dashboard status | Worktree/branch mapping, as in Subagent fields. Skip a step that needs the cloud agent itself, such as its dashboard status or its own machine |
| A built-in PR tool | Use the forge command the step gives, such as `gh pr create`. That is not a skip, so write no line |
| `/loop` (run until a predicate holds) | No equivalent. Drive Autonomous-run playbooks by continuing the session / re-invoking the command until the check passes |
| `make-bot-ui` (whole skill) | Cursor Automations-only (webhook routines, `update_state`, `api2.cursor.sh`). Skip entirely on OpenCode |
| Cursor's built-in `/babysit` (referenced by poteto-help as the non-poteto-mode route) | No equivalent installed. Route "babysit this PR" requests to `/poteto-mode`, which owns the Babysit playbook |
| Custom Modes (Option+Enter keeps `/poteto-mode` on) | No equivalent. Start each task with `/poteto-mode` |
| `/deslop` before commit (cursor-team-kit) | Installed at `.opencode/skills/deslop/` from upstream `cursor-team-kit`. Run `/deslop` as the playbooks prescribe |
| `control-cli` / `control-ui` for shipping UI/IDE/CLI | Skip with the line. Verify with this repo's own harness instead (`dotnet test`, Playwright) |
| Cursor's built-in `create-skill` / `/create-skill` | Author `SKILL.md` by hand: folder name = skill name, frontmatter `name` + `description`, per the Agent Skills format. `automate-me` output skills go under `.opencode/skills/` |
