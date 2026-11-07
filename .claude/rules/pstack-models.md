# pstack model configuration. One line per role. Delete a line to fall back to the skill default.
# `inherit-parent` or `auto` as a value: the role runs on the parent chat model (omit Agent `model`). Alias entries in a panel list still count toward its fan-out.
# Values are `<model> <effort>` for the Claude Code `Agent` tool (`model`: opus, sonnet, haiku, fable; `effort`: low, medium, high, xhigh, max). Cursor slugs are not valid here.
# budget: large (xhigh)
# tiers: haiku runs high-volume, low-judgment fan-out (swarm workers, code explorers). sonnet writes code: features, fixes, refactors, and plans handed down from opus. opus plans, reviews, investigates, and judges, where tokens are few and accuracy matters most.
# parent session: run the interactive session on opus; role lines below only affect subagents.
# cross-judge: every entry is the Claude family, so arena skips the cross-family judge and the parent picks the base alone (arena Phase C).
feature, refactoring: sonnet xhigh
bug-fix: sonnet xhigh
perf-issue: sonnet xhigh
hillclimb: sonnet xhigh
judgment and prose: opus xhigh
hardest tasks: opus xhigh
how explorer: haiku xhigh
how explainer: opus xhigh
why investigators: sonnet xhigh
why synthesizer: opus xhigh
reflect tooling: sonnet xhigh
reflect judgment, divergent, synthesizer: opus xhigh
arena runners: opus xhigh, sonnet xhigh
arena cross-judge pool: opus xhigh, sonnet xhigh
swarm workers: haiku xhigh
architect runners: opus xhigh, sonnet xhigh
interrogate reviewers: opus xhigh, sonnet xhigh
