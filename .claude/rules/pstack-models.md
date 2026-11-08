# pstack model configuration. One line per role. Delete a line to fall back to the skill default.
# `inherit-parent` or `auto` as a value: the role runs on the parent chat model (omit Agent `model`). Alias entries in a panel list still count toward its fan-out.
# Values are `<model> <effort>` for the Claude Code `Agent` tool (`model`: opus, sonnet, haiku, fable; `effort`: low, medium, high, xhigh, max). Cursor slugs are not valid here.
# budget: large (xhigh)
# tiers: haiku runs high-volume, low-judgment fan-out (swarm workers, code explorers). sonnet carries code, planning, review, and judgment. opus runs the hardest tasks (cross-cutting design, gnarly concurrency, subtle algorithms) and the panel roles that guide other agents: arena runners compete on candidates the parent picks and grafts from, architect runners sketch the design that sonnet implements, and interrogate reviewers prove bugs without fixing them.
# parent session: opus xhigh is the orchestrator. It runs every user prompt and the playbooks, and delegates to the roles below. `.claude/settings.json` pins it; role lines below only affect subagents.
# cross-judge: every entry is the Claude family, so arena skips the cross-family judge and the parent picks the base alone (arena Phase C).
feature, refactoring: sonnet xhigh
bug-fix: sonnet xhigh
perf-issue: sonnet xhigh
hillclimb: sonnet xhigh
judgment and prose: sonnet xhigh
hardest tasks: opus xhigh
how explorer: haiku xhigh
how explainer: sonnet xhigh
why investigators: sonnet xhigh
why synthesizer: sonnet xhigh
reflect tooling: sonnet xhigh
reflect judgment, divergent, synthesizer: sonnet xhigh
arena runners: opus xhigh
arena cross-judge pool: sonnet xhigh
swarm workers: haiku xhigh
architect runners: opus xhigh
interrogate reviewers: opus xhigh
