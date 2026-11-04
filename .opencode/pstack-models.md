# pstack model configuration. One line per role. Delete a line to fall back to inherit-parent (the parent chat model).
# `inherit-parent` or `auto` as a value: the role runs on the parent chat model (omit the task `model`). Alias entries in a panel list still count toward its fan-out.
# Values are OpenCode model IDs (`provider/model-id`, see `opencode models`). Cursor slugs (claude-opus-5-5-xhigh, grok-4.7-xhigh-fast) are not valid here.
# budget: large (xhigh)
feature, refactoring: inherit-parent
bug-fix: inherit-parent
perf-issue: inherit-parent
hillclimb: inherit-parent
judgment and prose: inherit-parent
hardest tasks: inherit-parent
how explorer: inherit-parent
how explainer: inherit-parent
why investigators: inherit-parent
why synthesizer: inherit-parent
reflect tooling: inherit-parent
reflect judgment, divergent, synthesizer: inherit-parent
arena runners: inherit-parent, inherit-parent
arena cross-judge pool: inherit-parent, inherit-parent
swarm workers: inherit-parent
architect runners: inherit-parent, inherit-parent
interrogate reviewers: inherit-parent, inherit-parent
