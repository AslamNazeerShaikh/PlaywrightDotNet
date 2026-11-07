# pstack on Claude Code

pstack (poteto's agent workflow plugin, [cursor/plugins](https://github.com/cursor/plugins/tree/main/pstack))
is installed project-locally for Claude Code from the
[backnotprop/pstack](https://github.com/backnotprop/pstack) mirror. It turns the agent into a rigorous
engineering team: match the task to a playbook, run the steps, verify against
the real artifact. The goal is less, higher-quality code. The OpenCode install
is separate and unchanged; see [17-pstack-opencode.md](17-pstack-opencode.md).

## Use

1. Optional: run `/setup-pstack` to pick a reasoning budget and per-role
   models. It rewrites `.claude/rules/pstack-models.md`, which Claude Code
   loads into every session. The committed sheet tiers roles by cost, all at
   `xhigh`: `haiku` for high-volume fan-out (swarm workers, `how` explorers),
   `sonnet` for writing code (feature, bug-fix, perf, hillclimb, `why`
   investigators, `reflect` tooling), and `opus` for planning, review, and
   judgment (prose, hardest tasks, explainers, synthesizers, panels). Panels
   pair `opus` with `sonnet`; with no second model family, arena skips the
   cross-family judge.
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

The source is [backnotprop/pstack](https://github.com/backnotprop/pstack), a
mirror of upstream `pstack/` that keeps an `upstream` branch with Cursor's
files and a `main` branch with harness-neutral rewrites on top. Vendored from
mirror `f104aa8` (upstream pstack 0.15.36). Over upstream it adds: skills that
name Claude Code's own tools, paths, and transcript folders instead of
Cursor's; a Harness section in `poteto-mode`; the `no-comments` `REVIEW` keep
for comments about an outside rule it cannot check
([#14](https://github.com/backnotprop/pstack/issues/14)); and the `statechart`
Windows fix ([#17](https://github.com/backnotprop/pstack/issues/17)).

| Mirror (`backnotprop/pstack`) | This repo |
| --- | --- |
| `skills/` (58 skills) | `.claude/skills/`, plus upstream's Claude Code adapter line in the 23 `SKILL.md` files upstream's generator wraps |
| `agents/` (`poteto-agent`, `comment-sicko`) | `.claude/agents/` (verbatim, plus a repo harness note on `poteto-agent`) |
| `docs/harness/claude-code.md`, `docs/guide/` | `.claude/docs/` (verbatim; `guide/07-overnight.md` takes upstream's generated copy, which links the automation pack on GitHub) |
| `LICENSE` | `.claude/pstack-LICENSE` |
| Upstream `harness/pstack/claude-code/README.md` | `.claude/README.md` |
| `cursor-team-kit/skills/deslop` (upstream) | `.claude/skills/deslop/` (verbatim) |
| `~/.claude/rules/pstack-models.md` | `.claude/rules/pstack-models.md` |
| Plugin-specific adapter rows (`pstack:` prefix, `~/.claude/plugins/`) | Overridden in the root `CLAUDE.md` |

The adapter line is the only edit to mirror files. It is what upstream's
`scripts/generate-pstack-harness-trees.mjs` inserts after each wrapped
skill's H1, so the tree matches the shape of upstream's Claude Code install.
Claude Code mappings live only in the adapter, the mirror's Harness section,
and the `CLAUDE.md` overrides, so a re-sync is a re-copy plus diff.
`.claude/settings.local.json` is gitignored, and `.graphifyignore` skips
`.claude/` because the graph already indexes the same corpus under `.opencode/`.

## Re-syncing with the mirror

Check the mirror's `SYNC-LOG.md` for a new upstream sync, then:

```sh
git clone --depth 1 https://github.com/backnotprop/pstack.git /tmp/pstack-bn
git clone --depth 1 --filter=blob:none --sparse https://github.com/cursor/plugins.git /tmp/pstack-up
git -C /tmp/pstack-up sparse-checkout set pstack harness/pstack/claude-code cursor-team-kit/skills/deslop
B=/tmp/pstack-bn U=/tmp/pstack-up R=<repo>/.claude
rsync -a --delete --exclude deslop $B/skills/ $R/skills/
rsync -a --delete $B/docs/ $R/docs/
cp $U/harness/pstack/claude-code/docs/guide/07-overnight.md $R/docs/guide/
cp $B/agents/comment-sicko.md $R/agents/
LINE='In Claude Code, read [the Claude Code adapter](../../docs/harness/claude-code.md) once per session before you follow this skill. It maps the Cursor names in pstack skills to Claude Code.'
for s in $(diff -rq $U/pstack/skills $U/harness/pstack/claude-code/skills | awk '{print $2}' | sed "s|$U/pstack/skills/||; s|/SKILL.md||"); do
  awk -v line="$LINE" 'done!=1 && /^# / {print; getline n; while (n ~ /^$/) getline n; print ""; print line; print ""; print n; done=1; next} {print}' $R/skills/$s/SKILL.md > /tmp/s && mv /tmp/s $R/skills/$s/SKILL.md
done
diff -q $U/cursor-team-kit/skills/deslop/SKILL.md $R/skills/deslop/SKILL.md
```

Run it in bash (zsh does not split the `$(...)` list). If upstream's
`docs/guide/07-overnight.md` no longer differs between `pstack/` and the
generated tree, drop that `cp`. If the mirror's `agents/poteto-agent.md`
changed, re-copy it and re-append the harness note from the current file's
last paragraph. If the adapter gained rows that assume the plugin install, add
the repo override to `CLAUDE.md`. Review `git diff` before committing: it
should show only the mirror's changes.

## Validation

After any change: every `.claude/skills/*/SKILL.md` has `name` equal to its
directory and a `description` of 1 to 1024 characters; every relative link in
`.claude/skills/` and `.claude/agents/` resolves; `.claude/agents/*.md` have
`name` and `description`; and a new session lists `/poteto-mode` and the
`poteto-agent` and `comment-sicko` agents.
