---
name: working-lean-with-poteto
description: Use when a poteto-mode task involves a bug, flake, multi-call-site change, or abstraction and dependency temptation under time pressure and only one slash command is allowed
---

# Working Lean With Poteto

## Overview

Poteto routes. Ponytail bounds the diff. Superpowers gates quality. One slash runs all three.

**REQUIRED:** poteto-mode triggers; superpowers:test-driven-development for every change.

## When to Use

- `/poteto-mode` plus bug, flake, retry, or timing symptom.
- Change touches 2+ call sites, or tempts new abstraction, wrapper, or dependency.
- Demo, deadline, or manager pressure to ship as-is.
- Prompt has `lean`, `ponytail`, `be lazy`, `yagni`, or names systematic debugging.

When NOT to use: pure read-only investigation. Single one-line edit.

## Core Pattern

One slash carries style; persistence replaces the second.

- `/poteto-mode` plus `lean` means ponytail full for the whole run.
- Bug or flake means superpowers:systematic-debugging before any fix.
- New feature or plan means superpowers:brainstorming first, smallest version only.
- Every behavior change means RED first: one failing test, watched fail, then pass.
- Done claim means superpowers:verification-before-completion on the real artifact (`dotnet test`, Playwright MCP).

## Quick Reference

| Poteto step | Required gate |
|-------------|---------------|
| Feature, Bug fix, Perf issue matched | state data shape first, architect only across a function boundary |
| Bug or flake | systematic-debugging trace, fix once in shared code |
| Prototype fork (which approach, what should this do) | Prototype sketch decides, never AskQuestion for observables |
| Diff shaping | ponytail smallest change: reuse helper, stdlib, installed dep, else minimum code |
| Commit or review | deslop then no-comments, then verification run |

## Implementation

1. Match playbook. Open steps verbatim as todos.
2. If bug or flake, trace to first wrong state. Grep every caller before editing.
3. Build smallest reuse-first diff. Deletion beats addition.
4. Leave one small test for non-trivial logic. Mark limits as `shortcut: <limit>, <when to upgrade>`.
5. Verify on real harness. End with skipped items and risks.

## Common Mistakes

- Two slashes in one prompt. Fix: keep `/poteto-mode` plus prose `lean`.
- Scope excuse ("only poteto was asked"). Fix: scope bounds deliverable, never verification. Urgency narrows change, never skips gates.
- Future-proofing abstraction. Fix: duplication-first, extract only on second real reuse.
- Tests after. Fix: delete untested code, restart RED. Manually tested still means untested.

## Rationalizations

| Excuse | Reality |
|--------|---------|
| "Only poteto was asked" | Poteto owns sequence, not quality waiver. Gates still apply. |
| "Demo urgency justifies ship now" | Urgency shrinks change, never skips root-cause or verification. |
| "Big abstraction is safer" | Speculation. Smallest reuse plus one test is safer. |
| "Tests after achieve same goals" | Tests after verify memory. RED first discovers gaps. |

## Red Flags - STOP

- "ship as-is", "clean later", "only poteto was asked"
- "just this once", "spirit not letter", "pragmatic not dogmatic"
- New dep for lines, wrapper for later, unasked option
