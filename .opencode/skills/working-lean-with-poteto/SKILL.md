---
name: working-lean-with-poteto
description: Use when a poteto-mode task involves a bug, flake, multi-call-site change, or abstraction and dependency temptation under time pressure and only one slash command is allowed
---

# Working Lean With Poteto

## Overview

Poteto routes. Ponytail bounds diff. Superpowers gates quality.

**REQUIRED:** poteto-mode; TDD every change; model from `pstack-models.md` role line.

## When to Use

- `/poteto-mode` plus bug, flake, or timing.
- 2+ call sites, or tempts abstraction, wrapper, dependency.
- Demo, deadline, or manager pressure to ship as-is.
- Prompt has `lean`, `yagni`, or names debugging.

Not: read-only investigation, one-line edit.

## Core Pattern

One slash carries style; persistence replaces the second.

- `/poteto-mode` plus `lean` means ponytail full.
- Bug or flake means systematic-debugging first.
- Feature or plan means brainstorming first, smallest only.
- Every behavior change means RED first: one failing test, watched fail, then pass.
- Done means verification-before-completion on real artifact.
- Spawn poteto-agent delegates, general for MCP edits, explore read-only. Model is the role line as `provider/model#variant`; omit when missing, `inherit-parent`, or `auto`.

## Quick Reference

| Poteto step | Required gate |
|-------------|---------------|
| Feature, Bug fix, Perf issue matched | state data shape first, architect only across a function boundary |
| Bug or flake | systematic-debugging trace, fix once in shared code |
| Prototype fork | sketch decides, never AskQuestion |
| Diff shaping | ponytail smallest change: reuse helper, stdlib, installed dep, else minimum code |
| Commit or review | deslop then no-comments, then verification run |
| Models | hardest/deepseek#max; bug-fix/inkling#max; chores/beam-free#max; missing omits model |
| Fix loop | Critical/Important/spec-miss: resume 1-3, fresh+bump 4-5, max 5, tests+report+re-review each; contested: one fresh bakeoff + owner verdict; Minor: ledger deferred |

## Implementation

1. Match playbook. Open steps verbatim as todos.
2. Bug or flake: trace to first wrong state, grep callers first.
3. Build smallest reuse-first diff. Deletion beats addition.
4. Leave one test for non-trivial logic. Mark limits `shortcut: <limit>, <when to upgrade>`.
5. Verify on real harness. End with skipped items and risks.

## Common Mistakes

- Two slashes. Fix: `/poteto-mode` plus prose `lean`.
- Scope excuse. Fix: scope bounds deliverable, never verification; urgency narrows change.
- Future-proofing abstraction. Fix: duplication-first, extract on second reuse.
- Tests after. Fix: delete, restart RED; manual still means untested.

## Rationalizations

| Excuse | Reality |
|--------|---------|
| "Only poteto was asked" | Sequence, not waiver. Gates apply. |
| "Demo urgency justifies ship now" | Urgency shrinks change, never skips gates. |
| "Big abstraction is safer" | Speculation. Smallest reuse plus test wins. |
| "Tests after achieve same goals" | Tests after verify memory. RED finds gaps. |

## Red Flags - STOP

- "ship as-is", "clean later", "only poteto was asked", "just this once", "spirit not letter"
- New dep for lines, wrapper for later
