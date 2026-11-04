# pstack on OpenCode

pstack (poteto's agent workflow plugin, [cursor/plugins](https://github.com/cursor/plugins/tree/main/pstack))
is installed project-locally for OpenCode. It turns the agent into a rigorous
engineering team: match the task to a playbook, run the steps, verify against
the real artifact. The goal is less, higher-quality code.

## Use

Two steps (mirrors upstream `get started`):

1. Run `/setup-pstack`: pick a reasoning budget and per-role models. It writes
   `.opencode/pstack-models.md` (auto-loaded every session via `instructions`
   in `.opencode/opencode.json`). Until you run it, every role runs on the
   parent chat model.
2. Use `/poteto-mode` at the start of any task that needs rigor. It matches
   your request to one of 23 playbooks, opens a todo list from the playbook's
   steps, and routes to the other skills as the steps need them.

```text
/poteto-mode this pr has a subtle bug where the scroll drifts every 750ms even when idle. repro first, then fix and verify.
```

Stuck, or unsure which skill fits? Ask `/poteto-help`.

## Layout

| Upstream (`pstack/`) | This repo |
| --- | --- |
| `skills/` (58 skills) | `.opencode/skills/` (verbatim copy; `../<name>/SKILL.md` links intact) |
| `agents/` (`poteto-agent`, `comment-sicko`) | `.opencode/agents/` (frontmatter converted to `mode: subagent`) |
| Slash invocation (`/poteto-mode`) | `.opencode/commands/` (34 commands, one per non-`principle-*` skill) |
| `docs/guide/`, `README.md` | `.opencode/docs/guide/`, `.opencode/README.md` (kept so skill-relative links resolve) |
| `docs/harness/claude-code.md` | Kept at `.opencode/docs/harness/claude-code.md` for reference; OpenCode sessions use `.opencode/docs/harness/opencode.md` instead |
| `~/.cursor/rules/pstack-models.mdc` | `.opencode/pstack-models.md` |
| `automations/benny` | Not copied. It runs on Cursor Automations (Cursor-only); see the adapter's skip rule |

Skill files are verbatim upstream copies so a future pstack update is a
re-copy plus diff. OpenCode-specific mappings (tools, subagents, models,
paths, skipped Cursor-only steps) live only in
`.opencode/docs/harness/opencode.md`, which sessions read once per task.
`cursor-team-kit` skills referenced by playbooks (`deslop`, `control-cli`,
`control-ui`) are not installed; those steps are skipped with a one-line note.

## Cross-check against pstack-claude (michael-denyer)

A third-party port ([pstack-claude](https://github.com/michael-denyer/pstack-claude))
targets Claude Code, Codex, Pi, Copilot, Muse, and (via `~/.agents/skills/`
symlinks) OpenCode. It was used to audit this install. Findings and deltas:

- **Our 58 skills are byte-identical to upstream `cursor/plugins` HEAD.**
  Their sync pin lags HEAD slightly; nothing in our tree is stale.
- **`deslop` was the one load-bearing gap.** Upstream `poteto-mode`,
  `multi-phase-plan`, and `opening-a-pr` invoke `/deslop` from
  `cursor-team-kit`, which we had not installed. Added verbatim from
  upstream (`cursor-team-kit/skills/deslop`) plus the `/deslop` command.
  `control-cli` / `control-ui` are referenced too but need Cursor IDE /
  browser harness tooling; like pstack-claude, we do not port them (see the
  adapter's skip rule) and verify CLI/UI work with `dotnet test` + Playwright.
- **`make-bot-ui` is kept, they drop it.** The skill is entirely
  Cursor-Automations-bound (webhook routines, `api2.cursor.sh`) and can never
  run here, so pstack-claude excludes it. We keep the verbatim copy so
  skill-relative links stay intact and re-syncs stay diff-clean; the adapter
  marks the whole skill skip-entirely.
- **Their standalone `babysit` skill is not ported.** It is port-authored
  (not upstream) and leans on Claude's `loop` skill. On OpenCode, PR
  shepherding stays with `/poteto-mode`'s Babysit playbook (see adapter).
- **Their extra bundle is not copied.** `fix-ci`, `fix-merge-conflicts`,
  `get-pr-comments`, `make-pr-easy-to-review`, `what-did-i-get-done`,
  `thermos`, and the thermo-nuclear reviews are unreferenced by pstack
  skills; they are easy verbatim adds from upstream if a workflow needs them.
- **Model-sheet shape agrees.** Their per-runtime sheet for OpenCode is the
  global `~/.config/opencode/pstack-models.md` loaded via `instructions`;
  ours is the project-local equivalent (matches the project-local skills),
  with the same role labels as the skills. Their extra roles
  (`strongest judgment`, `thermos reviewers`) are port-specific and unused by
  verbatim skills.
- **Routing agrees.** OpenCode has no SessionStart hook, so both ports fall
  back to a standing instruction: ours is the `AGENTS.md` section above.

## Re-syncing with upstream

Skill files are verbatim copies of `cursor/plugins` HEAD plus verbatim
`deslop` from `cursor-team-kit`. To update (deslop aside, everything lives
under `pstack/` upstream):

```sh
git clone --depth 1 --filter=blob:none --sparse https://github.com/cursor/plugins.git /tmp/pstack-up
cd /tmp/pstack-up && git sparse-checkout set pstack/skills pstack/docs pstack/README.md pstack/LICENSE cursor-team-kit/skills/deslop
diff -rq pstack/skills/ <repo>/.opencode/skills/ | grep -v "Only in <repo>"
```

Copy over changed files (never rename: `name` must equal the directory),
re-run the validation below, and regenerate `/deslop` only if its
frontmatter changed. Upstream `plugin.json`/`docs/harness/claude-code.md`
are reference-only here.

## Validation

After any skill change, re-run: frontmatter `name` == directory, lowercase
hyphenated, `description` 1–1024 chars, every `/<command>` has a matching
skill, every `../<skill>/SKILL.md` and `../../docs|README` link resolves
under `.opencode/`, `python3 -c "import json; json.load(open('.opencode/opencode.json'))"`,
and `opencode debug agents` lists `poteto-agent` + `comment-sicko`.

## Conventions

- Spawn playbook delegates as `poteto-agent` (`task` tool,
  `subagent_type: "poteto-agent"`), fresh per unit of work with consolidated
  scope. Never run the playbook itself in a `poteto-agent`.
- Read-only investigation goes to `explore`; implementation delegates go to
  `general` unless the skill prescribes otherwise.
- Every role line in `.opencode/pstack-models.md` accepts an OpenCode model ID
  or `inherit-parent` (run on the parent model). Re-run `/setup-pstack` after
  `opencode models` changes.
