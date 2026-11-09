---
name: working-lean-with-poteto
description: Use when a /poteto-mode prompt says lean or yagni, or a task involves a bug, flake, multi-call-site change, or abstraction and dependency temptation under time pressure
---

# Working Lean With Poteto

## Overview

Poteto routes. Ponytail bounds diff. Superpowers gates quality. Gates are superpowers-plugin skills; `ponytail` is repo-local. Load means the skill tool by ID. The agent is the loader.

**REQUIRED:** poteto-mode; invoke `ponytail` full first, then each fired gate by ID; TDD every change; model from `pstack-models.md`.

## When to Use

- `/poteto-mode` plus bug, flake, or timing.
- 2+ call sites, or tempts abstraction/wrapper/dependency.
- Demo/deadline pressure to ship as-is.
- Prompt says `lean`, `yagni`, or debugging.

Not: read-only investigation, one-line edit.

## Core Pattern

- `lean` means invoke `ponytail` full first.
- Bug or flake means invoke `systematic-debugging` first.
- Feature or plan means invoke `brainstorming` first, smallest only.
- Every behavior change means RED first per `test-driven-development`, one test watched fail then pass.
- Done means invoke `verification-before-completion` on real artifact.
- Spawn poteto-agent delegates, general for MCP, explore read-only. Every brief carries this file path + role model string. Model is role line `provider/model#variant`; omit when missing/`inherit-parent`/`auto`.

## Quick Reference

| Poteto step | Required gate |
|-------------|---------------|
| Feature/Bug/Perf matched | state data shape first, architect only across boundary |
| Bug/flake | trace to first wrong state, fix once shared |
| Prototype fork | sketch decides, never AskQuestion |
| Diff | smallest reuse: helper, stdlib, dep, else minimum |
| Commit/review | deslop, no-comments, verification run |
| Models | hardest/deepseek#max; feature, bug-fix, perf-issue/inkling#max; chores/beam-free#max; missing omits model |
| Fix loop | Critical/Important/spec-miss: resume 1-3, fresh+bump 4-5, max 5, tests+report+re-review; contested: one bakeoff; unclear decidable: decide+ledger; undecidable: ask once; Minor: deferred |
| Plan gate | 2+ tasks/vague/shared names: names + Consumes/Produces + test per task first; one-line skips; coupled runs sequential |

## Implementation

1. Match playbook; open steps as todos. Plan gate: names + tests first; conflicts as `Ruling: <decided> — <why>`.
2. Bug/flake: trace to first wrong state, grep callers.
3. Smallest reuse-first diff; deletion wins.
4. One test for non-trivial logic; mark `shortcut: <limit>, <when to upgrade>`.
5. Verify on real harness. End with skipped items and risks.

## Common Mistakes

- Two slashes. Fix: `/poteto-mode` plus `lean`.
- Scope excuse. Fix: scope bounds deliverable, never verification.
- Future-proofing. Fix: duplication first, extract on reuse.
- Tests after. Fix: delete, restart RED.

## Rationalizations

| Excuse | Reality |
|--------|---------|
| "Only poteto was asked" | Sequence, not waiver. |
| "Demo urgency justifies ship now" | Urgency shrinks change, never skips gates. |
| "Big abstraction is safer" | Speculation. Reuse plus test wins. |
| "Tests after achieve same goals" | Tests after verify memory. RED finds gaps. |

## Red Flags - STOP

- "ship as-is", "clean later", "only poteto was asked", "just this once", "spirit not letter"
- New dep for lines, wrapper for later
