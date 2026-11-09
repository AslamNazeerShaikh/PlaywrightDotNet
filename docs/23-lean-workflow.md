# Run the lean workflow with one slash

Use the `working-lean-with-poteto` skill to run poteto-mode, ponytail, and superpowers quality gates from a single `/poteto-mode` prompt. The skill file is `.opencode/skills/working-lean-with-poteto/SKILL.md`.

Use it when a `/poteto-mode` prompt says `lean` or `yagni`. Also use it when the task involves a bug, a flake, or a timing symptom. Also use it when the change touches two or more call sites. Also use it when the work tempts a new abstraction, wrapper, or dependency. Also use it under demo, deadline, or manager pressure to ship as-is.

Skip this for a read-only investigation or a one-line edit.

## Type one slash

To start a task, type `/poteto-mode` plus the word `lean` and the task:

```text
/poteto-mode lean fix the flaky login retry
```

The word `lean` loads ponytail at full before any other step. Do not type `/ponytail` as well. OpenCode reads only the first word as a command. A second `/name` becomes plain text. The trigger matches anywhere in the request, not just on the first line.

No code in the task (git moves, deletes, status checks, one-shot shell): skip the gates below. Run it inline with no subagent and confirm in two lines.

## Follow the gates

The skill keeps the poteto playbook sequence and adds gates. They fire on their trigger:

1. Bug or flake: trace with `systematic-debugging` to the first wrong state. Grep every caller. Fix once in shared code.
2. Feature or plan: run `brainstorming` first. Build the smallest version that does the core job.
3. Matched Feature, Bug fix, or Perf issue playbook: state the data shape first. Call `architect` only across a function boundary.
4. Approach fork: run the Prototype playbook sketch. Never ask the user to pick an observable fact.
5. Every behavior change: write one failing test first. Watch it fail. Then write the minimum code. Watch it pass.
6. Done claim: verify on the real harness. For the backend, run `dotnet test` in `src/backend` (`JobScrape.slnx`). For the frontend, run `bun test` or `bun run test:e2e` in `src/frontend`. End the reply with skipped items and risks.

Spawn `poteto-agent` for delegated work. Use `general` for edits that need MCP servers. Use `explore` for read-only checks.

## Write a plan only when the plan gate fires

Write task names plus `Consumes` and `Produces` plus one test per task before code only when one of these is true:

- The work has 2 or more tasks.
- The request is vague or has no acceptance.
- Two or more call sites share names or interfaces.

Skip the plan for a one-line fix. If the tasks are coupled through shared files, names, or state, run them in order in one context. Never fan them out to parallel workers. Record each conflict as `Ruling: <decided> — <why>`.

## Pick the model for each delegate

Read the role line in `.opencode/pstack-models.md` for each delegate. Pass it as `provider/model#variant`:

| # | Role line | Model |
|---|-----------|-------|
| 1 | `hardest tasks`: redesign, cross-cutting change | `opencode-go/deepseek-v4.1-flash#max` |
| 2 | `feature`, `refactoring`, `bug-fix`, `perf-issue` | `opencode/inkling-2-small-free#max` |
| 3 | `chores`: pull, clean, format, graphify update | `opencode-go/beam-free#max` |

Omit `model` when the role line is missing or says `inherit-parent` or `auto`. Never use Cursor slugs such as `claude-opus-5-5-xhigh`. They are Cursor IDs and are not valid in this repo model map.

## Run the Fix loop

| # | Finding | Loop |
|---|---------|------|
| 1 | Critical, Important, or spec miss | Resume the same delegate for rounds 1 to 3. Use a fresh delegate with a stronger model for rounds 4 to 5. Stop at 5. Each round ships tests, a short report, and a re-review. |
| 2 | Contested design | Run one fresh bakeoff and take the verdict. |
| 3 | Minor | Record it as deferred. |

## Close the run

Run `/deslop` before commit and reread the diff. Run `/no-comments` before review. Commit with a Conventional Commits message per `docs/13-conventional-commits.md`.

## Reference: the three packs and what the lean skill calls

This appendix is lookup reference. It lists the pieces this workflow calls and where each piece lives.

### Pack 1: pstack core (project-local)

Pstack ships inside the repo under `.opencode/`. It is a manual sync of `backnotprop/pstack` at `a6342ded`. See `docs/17-pstack-opencode.md` and `.opencode/SYNC-LOG.md`. There is no global harness config (skills, agents, commands, plugins, instructions) in `~/.config/opencode/`. It holds only CLI/UI settings. Project config is `.opencode/` plus `AGENTS.md`, plus plugin skills loaded through the superpowers entry in `.opencode/opencode.json`.

Router: `.opencode/skills/poteto-mode/SKILL.md` with 23 playbooks in `.opencode/skills/poteto-mode/playbooks/`. Slash entry is `.opencode/commands/poteto-mode.md`, which loads the skill and reads `.opencode/docs/harness/opencode.md` once per session for tool mappings. Pstack itself is documented in `docs/17-pstack-opencode.md`. See also `docs/18-harness-packs.md` and `.opencode/SYNC-LOG.md`.

Playbooks poteto-mode can route to, with the file that owns each step list. The lean skill names Feature, Bug fix, Perf issue, and Prototype directly. The rest arrive through poteto-mode routing:

| # | Playbook | File | Steps the lean skill relies on |
|---|----------|------|-------------------------------|
| 1 | Feature | `playbooks/feature.md` | name the data shape, throughput checkpoint |
| 2 | Bug fix | `playbooks/bug-fix.md` | reproduce on the same surface, root-cause, fix, verify |
| 3 | Refactoring | `playbooks/refactoring.md` | behavior-preserving change, verify callers |
| 4 | Perf issue | `playbooks/perf-issue.md` | baseline before, measure after |
| 5 | Investigation | `playbooks/investigation.md` | read-only answer with cited evidence |
| 6 | Prototype | `playbooks/prototype.md` | sketch to settle an observable fork |
| 7 | Hillclimb | `playbooks/hillclimb.md` | one metric, one commit per accepted win |
| 8 | Runtime forensics | `playbooks/runtime-forensics.md` | diagnose from live instrumentation |
| 9 | Trace forensics | `playbooks/trace-forensics.md` | diagnose from a captured artifact |
| 10 | Visual parity | `playbooks/visual-parity.md` | pixel-exact equivalence |
| 11 | Multi-phase plan | `playbooks/multi-phase-plan.md` | phases across stacked PRs |
| 12 | Orchestrate | `playbooks/orchestrate.md`, matched by poteto-mode prose routing, not by slash | standing multi-day program |
| 13 | Autopilot-full | `playbooks/autopilot-full.md` | queue of PRs to merged |
| 14 | Autopilot-stack | `playbooks/autopilot-stack.md` | queue of PRs built, operator lands |
| 15 | Babysit | `playbooks/babysit.md` | drive a PR or stack to merge-ready |
| 16 | Shipping | `playbooks/shipping.md` | verify a green stack, land bottom-up |
| 17 | Autonomous run | `playbooks/autonomous-run.md` | drive one task to a predicate |
| 18 | Session pickup | `playbooks/session-pickup.md` | resume in-flight work |
| 19 | Pause safely | `playbooks/pause-safely.md` | suspend work for later resume |
| 20 | Authoring a skill | `playbooks/authoring-a-skill.md` | write or edit a `SKILL.md` |
| 21 | Eval | `playbooks/eval.md` | test a skill or structure change |
| 22 | Worktree cleanup | `playbooks/worktree-cleanup.md` | prune worktrees and simulators |
| 23 | Opening a PR | `playbooks/opening-a-pr.md` | runs at the end of every playbook |

Skills the lean workflow uses, all under `.opencode/skills/<name>/`. The lean skill names `architect`, `deslop`, and `no-comments` directly. `reflect` runs only on request. The rest arrive through poteto-mode routing:

| # | Skill | Lean use |
|---|-------|----------|
| 1 | `how` | nontrivial change or architecture doubt |
| 2 | `architect` | code crossing a function boundary, parallel design |
| 3 | `swarm` | parallel fan-out: coverage, races, partitions |
| 4 | `arena` | design or code bakeoff with base selection |
| 5 | `interrogate` | contested design before shipping |
| 6 | `deslop` | before commit, reread the diff |
| 7 | `no-comments` | before review, via `comment-sicko` agent |
| 8 | `technical-writing` | docs, RFCs, PR bodies |
| 9 | `figure-it-out` | no bundled playbook fits |
| 10 | `show-me-your-work` | decision trail for long or unsupervised work |
| 11 | `reflect` | on request (`/reflect`), second-opinion review pass |
| 12 | `unslop` | every prose surface including the reply |

`poteto-agent` (delegated work inside a playbook step) and `comment-sicko` (review-driven comment deletion) live in `.opencode/agents/`. `general` and `explore` are harness built-ins. Eight ECC agents in `.opencode/opencode.json` (`planner`, `code-reviewer`, `security-reviewer`, `tdd-guide`, `build-error-resolver`, `e2e-runner`, `doc-updater`, `refactor-cleaner`) back the `/plan`, `/code-review`, `/security`, `/build-fix`, `/e2e`, `/refactor-clean`, `/orchestrate`, and `/test-coverage` commands.

### Pack 2: superpowers plugin

Plugin line in `.opencode/opencode.json`: `superpowers@git+https://github.com/obra/superpowers.git`, installed at `22f0b46`. It ships a plugin that registers skills. No slash commands. No subagent definitions. Load with the `skill` tool by ID.

| # | Skill ID | Fires when | Lean use |
|---|----------|------------|----------|
| 1 | `using-superpowers` | every session | the 1% rule: check for a skill before any response |
| 2 | `brainstorming` | before any creative work or plan | yes, gate 2 above |
| 3 | `systematic-debugging` | before any bug-fix hypothesis | yes, gate 1 above |
| 4 | `test-driven-development` | before any implementation | yes, always, gate 5 above |
| 5 | `writing-plans` | spec exists, multi-step task, before code | yes, through the plan gate |
| 6 | `executing-plans` | inline plan execution, no subagent | yes, coupled-sequential path |
| 7 | `subagent-driven-development` | plan with independent tasks | yes, Fix loop rounds 1 to 5 |
| 8 | `requesting-code-review` | task done, feature done, before merge | yes, before merge-readiness |
| 9 | `receiving-code-review` | on review feedback, before applying it | yes, verify each item, one at a time |
| 10 | `verification-before-completion` | before claiming done | yes, gate 6 above |
| 11 | `finishing-a-development-branch` | impl done, tests green, decide integrate | no, poteto Shipping owns landing |
| 12 | `using-git-worktrees` | feature work needing isolation | no, poteto worktree mapping owns it |
| 13 | `dispatching-parallel-agents` | independent tasks | no, poteto `swarm` owns fan-out |
| 14 | `writing-skills` | create or edit a skill | authoring only, not runs |
| 15 | `diagnosing-superpowers` | a session went wrong | audit only, not runs |

### Pack 3: ponytail

Skill at `.opencode/skills/ponytail/SKILL.md`, from DietrichGebert/ponytail (`@dietrichgebert/ponytail`, MIT, attribution at `.opencode/ponytail-LICENSE`). The vendored copy asks the model to stay active for the whole session until `stop ponytail` or `normal mode`, but only as prose. Level persistence across turns needs the ponytail plugin, which is not in `opencode.json`. Slash entry is `.opencode/commands/ponytail.md`. Related commands: `/ponytail-audit`, `/ponytail-debt`, `/ponytail-gain`, `/ponytail-help`, `/ponytail-review`.

| # | Level | Behavior |
|---|-------|----------|
| 1 | `lite` | Build what was asked. Name the smaller option in one line and let the user pick. |
| 2 | `full` | The smallest complete change. Default. |
| 3 | `ultra` | Also question the request before building. |

Triggers from its description: any coding task (writing, fixing, refactoring, reviewing, choosing dependencies) and the words `ponytail`, `be lazy`, `simplest solution`, `yagni`, or complaints about over-engineering or bloat.

The lean skill takes three things from ponytail: the smallest-complete-change order (needed existence, reuse helper, stdlib, installed dep, one line, else minimum), the `shortcut: <limit>, <when to upgrade>` comment for known limits, and the closing line with skipped items and risks. It takes the level from the word `lean`, which maps to `full`.

### Call map: which lean row comes from where

| # | Lean element | Source pack and file |
|---|--------------|----------------------|
| 1 | playbook match, data shape, architect-across-boundary | pstack, `.opencode/skills/poteto-mode/SKILL.md` |
| 2 | systematic-debugging trace, fix once shared | superpowers, `systematic-debugging/SKILL.md` |
| 3 | brainstorming smallest-only | superpowers, `brainstorming/SKILL.md` |
| 4 | prototype sketch, never AskQuestion | pstack, `playbooks/prototype.md` |
| 5 | RED-first every behavior change | superpowers, `test-driven-development/SKILL.md` |
| 6 | plan gate names plus tests, sequential, rulings | superpowers, `writing-plans/SKILL.md` |
| 7 | smallest reuse-first diff, `shortcut:` limits | ponytail, `.opencode/skills/ponytail/SKILL.md` |
| 8 | models per role line | repo map, `.opencode/pstack-models.md` |
| 9 | Fix loop rounds | superpowers, `subagent-driven-development/references/fix-loop.md` |
| 10 | contested bakeoff | pstack, `arena` skill plus `interrogate` before shipping |
| 11 | deslop, no-comments, verification run | pstack triggers plus superpowers `verification-before-completion` |
| 12 | closing skipped plus risks line | ponytail, `SKILL.md:15` |

### Verify the claims in this document

```bash
ls .opencode/skills/poteto-mode/playbooks/ | wc -l
ls .opencode/commands | grep -E "poteto-mode|ponytail|deslop|no-comments|code-review"
ls .opencode/skills/ | grep -E "working-lean-with-poteto|^ponytail$|^poteto-mode$"
grep -c "lean" .opencode/skills/working-lean-with-poteto/SKILL.md
wc -w .opencode/skills/working-lean-with-poteto/SKILL.md
(cd src/backend && dotnet test --no-build)
```

`--no-build` needs a prior `dotnet build` on a clean clone.

## For maintainers

The skill file keeps to a 500-word budget. Check it with `wc -w .opencode/skills/working-lean-with-poteto/SKILL.md` before each skill commit. The count is a skill-authoring invariant, not part of running a task.
