---
name: setup-pstack
description: Configure which models pstack uses per role and at what reasoning budget. Detects your available models and writes the project model map. Use for /setup-pstack, "configure pstack models", "pstack budget", or changing pstack's model choices.
---

# Setup pstack

Write `.opencode/pstack-models.md`, one line per role. OpenCode loads it automatically through `opencode.json` instructions, so it applies to new sessions. There is no always-applied rule file in OpenCode.

## Steps

### 1. Detect available models

Run `opencode models` and list the `provider/model-id` values you can pass to a subagent. The subagent tool omits `model` for `inherit-parent` and `auto` roles. If you cannot detect any, ask the user to paste the IDs they have access to. Never write a real ID you have not confirmed is available. The aliases `inherit-parent` and `auto` are always valid even though they are not detected IDs.

### 2. Load current state

The role list below is the mapping. If `.opencode/pstack-models.md` already exists, read it and treat its `# budget` line and its role values as the current choices. Otherwise start from `inherit-parent` on every role. A line whose role is not in step 5 is retired. Drop it.

### 3. Budget, map, and confirm

**(a) Ask for a budget.** Prefer the `question` tool over free text. Offer these four options with these exact labels, and name the current budget when the file records one. With no rule, say that `large` matches the skill defaults.

- `unlimited — strongest reasoning model available`
- `large — default (parent model everywhere)`
- `medium — a cheaper or faster model for routine roles`
- `small — cheapest for routine roles, strongest reserved for hardest tasks and judgment`

**(b) Apply it.** Build the working table from the detected IDs. Keep any role whose current value names a real ID only when nothing about the budget forces a change. `inherit-parent` and `auto` do not change. For panel roles (arena runners, architect runners, interrogate reviewers) the value is a list, and one subagent runs per entry, alias entries included, so the list length sets the count. `arena cross-judge pool` is also a list, but Arena runs one judge. It tries only values from a family other than the parent's, in list order, so order it by preference. An `inherit-parent` or `auto` value never judges. `swarm workers` is the default model for every worker unless a race or comparison assigns another model per arm.

**(c) Show the roles and confirm.** Show every role with its model, marking any real ID not in the detected set as needing a choice. Also list each line step 2 dropped. Ask whether to accept as-is or change specific roles, offering the detected models plus `inherit-parent` and `auto` (both mean: this role runs on the parent chat model). Prefer the `question` tool over free text.

### 4. Validate

Every real ID written must be in the detected set. `inherit-parent` and `auto` always pass. If a chosen real ID is not available, stop and ask again.

### 5. Write the rule

Write `.opencode/pstack-models.md` with a `# budget` line with the chosen label and one line per role, using the same labels poteto-mode uses. Overwrite the whole file so re-runs stay idempotent. Shape:

```
# pstack model configuration. One line per role. Delete a line to fall back to inherit-parent (the parent chat model).
# `inherit-parent` or `auto` as a value: the role runs on the parent model (omit the subagent `model`). Alias entries in a panel list still count toward its fan-out.
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
```

### 6. Confirm

Tell the user which file was written. It applies to new sessions. Re-running this skill updates it.

### 7. Offer a verification skill (optional)

Check whether the project has a way to drive the real app for proof (a `verify-*` skill, or an existing harness). If not, offer once: "want a project-local verification skill, so agents can drive the app the way a user does and prove changes work? I can generate one with /create-verification-skill." On yes, invoke `/create-verification-skill` (resolves to `.opencode/commands/create-verification-skill.md`). On no, move on without pushing.

### 8. Offer a formal verification skill (optional)

Check whether the project already has a formal verification skill (a project skill that runs a model checker, a proof, or another verifier against the code). If not, offer once: "want a formal verification skill, so agents can prove a key property of your code holds in every case in scope, not only the cases tests reach? I can generate one with /create-formal-verification-skill." On yes, invoke `/create-formal-verification-skill` (resolves to `.opencode/commands/create-formal-verification-skill.md`). On no, move on without pushing.
