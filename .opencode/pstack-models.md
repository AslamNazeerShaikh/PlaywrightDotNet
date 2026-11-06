# pstack model configuration. One line per role. Delete a line to fall back to inherit-parent (the parent chat model).
# `inherit-parent` or `auto` as a value: the role runs on the parent chat model (omit the task `model`). Alias entries in a panel list still count toward its fan-out.
# Values are OpenCode model IDs (`provider/model-id`, see `opencode models`). Cursor slugs (claude-opus-5-5-xhigh, grok-4.7-xhigh-fast) are not valid here.
# budget: large (xhigh)
# thinking: muse-spark runs #high, beam-free runs #max, deepseek-v4.1-flash runs #max. DeepSeek carries hardest tasks, why synthesizer, architect runners, arena cross-judge pool, and interrogate reviewers.
# parent session: run the interactive session on opencode-go/muse-spark-1.3-contributor#high; role lines below only affect subagents.
feature, refactoring: opencode-go/beam-free#max
bug-fix: opencode-go/beam-free#max
perf-issue: opencode-go/beam-free#max
hillclimb: opencode-go/beam-free#max
judgment and prose: opencode-go/muse-spark-1.3-contributor#high
hardest tasks: opencode-go/deepseek-v4.1-flash#max
how explorer: opencode-go/beam-free#max
how explainer: opencode-go/muse-spark-1.3-contributor#high
why investigators: opencode-go/beam-free#max
why synthesizer: opencode-go/deepseek-v4.1-flash#max
reflect tooling: opencode-go/beam-free#max
reflect judgment, divergent, synthesizer: opencode-go/muse-spark-1.3-contributor#high
arena runners: opencode-go/beam-free#max, opencode-go/muse-spark-1.3-contributor#high
arena cross-judge pool: opencode-go/muse-spark-1.3-contributor#high, opencode-go/deepseek-v4.1-flash#max
swarm workers: opencode-go/beam-free#max
architect runners: opencode-go/deepseek-v4.1-flash#max, opencode-go/muse-spark-1.3-contributor#high
interrogate reviewers: opencode-go/deepseek-v4.1-flash#max, opencode-go/muse-spark-1.3-contributor#high
