# SYNC-LOG.md

Tracks upstream syncs of the project-local pstack core in `.opencode/`.
Source of truth: https://github.com/backnotprop/pstack (standalone mirror of `cursor/plugins/pstack`).
Vendor packs (dotnet, testing, UI, graphify) and ECC are synced separately; see `docs/18-harness-packs.md`.
The `.claude/` tree is a separate port checkout and is never touched by these syncs.

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
