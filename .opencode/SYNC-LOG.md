# SYNC-LOG.md

Tracks upstream syncs of the project-local pstack core in `.opencode/`.
Source of truth: https://github.com/backnotprop/pstack (standalone mirror of `cursor/plugins/pstack`).
Vendor packs (dotnet, testing, UI, graphify) and ECC are synced separately; see `docs/18-harness-packs.md`.
The `.claude/` tree is a separate port checkout and is never touched by these syncs.

## 2026-11-07 — mirror f104aa8 (0.15.36), OpenCode V2 (2.2.6) audit

- Global OpenCode config removed (`~/.config/opencode/`, backed up to `~/opencode-global-config-backup-2026-11-07.tar.gz`). Config is repo-only now.
- Synced 26 files that were still raw upstream to the mirror (architect, arena, automate-me, create-verification-skill, how, interrogate, ketchup, maintain-verification-skill, poteto-help, poteto-mode playbooks and `scripts/worktree-audit.sh`, recall, reflect and its reviewer references, show-me-your-work, statechart scripts, swarm, why).
- Rewrote the mirror's V1 OpenCode lines for V2: `task`/`subagent_type` to `subagent`/`agent`, settings file `.opencode/pstack-models.md`, project skill folder `.opencode/skills/`, transcripts via `opencode session list`/`export`, and repo-local install text in poteto-help.
- `poteto-mode`, `agents/poteto-agent.md`, `no-comments`: `poteto-agent` and `comment-sicko` are registered V2 subagents (verified with `opencode debug agents`); spawn them by `agent` ID.
- `agents/comment-sicko.md`: body now equals the mirror (completes the REVIEW sync); dropped `edit: deny`, which blocked its comment deletions. `skills/no-comments/references/comment-sicko.md` synced too.
- `setup-pstack`, `pstack-models.md` comments, `docs/harness/opencode.md`: role values are `provider/model#variant`; the adapter now maps to V2 tools (`subagent`, `skill` with `id`, `shell`, `background`, `sessionID`).
- `opencode.json`: V1 keys migrated to V2 (`agents`, `system`, `permissions` arrays, `model#variant`); removed the `command` block, which Markdown commands always replaced; dropped MCP `enabled`. Commands use `subagent: true`, and four ECC commands gained frontmatter.

## 2026-11-06 — mirror 0.15.36

- `skills/poteto-mode/SKILL.md`: pulled upstream wording fixes (agent-facing prose rule, deslop line, control-skill line, babysit line, pause-safely line, rejected-slug protocol). Appended an OpenCode-only Harness section adapted from upstream. Kept the project tables rule in Writing the reply.
- `skills/setup-pstack/SKILL.md`: rewrote OpenCode-only. Detection via `opencode models`, settings file `.opencode/pstack-models.md`, budget ladder maps to model choice, `question` tool for prompts. Role list unchanged (17 roles, all `inherit-parent`).
- `skills/no-comments/SKILL.md`: adopted upstream steps 1-2 (general-subagent fallback, REVIEW keeps, go on with accepted findings after a second rejection).
- `skills/no-comments/references/comment-sicko.md`: added from upstream (was missing; step 1 points at it).
- `agents/comment-sicko.md`: added the upstream REVIEW mercy paragraph and REVIEW keeps to the report line. Kept OpenCode frontmatter (`mode: subagent`, edit deny).
- `agents/poteto-agent.md`: corrected the harness note (no `poteto-agent` subagent type here; use `general`/`explore`).
- `skills/statechart/`: SKILL.md already matches upstream; scripts already TypeScript. Renamed package `@cursor-skill/statechart` to `@pstack/statechart`.
- `opencode.json`: registered `harness-audit`, `model-route`, `quality-gate`, `loop-start`, `loop-status` (agentless, matching the existing pattern).
- `commands/learn.md`, `eval.md`, `verify.md`, `checkpoint.md`, `skill-create.md`: removed orphan `agent: build` frontmatter (no such agent is registered).
- `commands/skill-create.md`: "Claude Code skills" now reads "agent skills".
- `commands/loop-start.md`: runbook path `.claude/plans/` now reads `.opencode/plans/`.
- `instructions/INSTRUCTIONS.md`: removed unregistered agent rows (`architect`, `go-*`, `database-reviewer`), pointed architecture decisions at `planner`, `/tdd` now reads `/test-coverage`, fixed the dangling hook-controls anchor, rewrote the Claude model strategy provider-neutral, fixed the header line.
- `README.md`: replaced the benny automation setup with an OpenCode-only note (pack not copied; no automation runtime here).
- Fork note: this tree is intentionally OpenCode-only and diverges from upstream verbatim where upstream names other harnesses. Future syncs are manual three-way merges against the mirror, not clean pulls.
