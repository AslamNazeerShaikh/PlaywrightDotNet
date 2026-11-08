# Harness Freshness Check-then-Sync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Report upstream deltas for the four harness packs and sync only meaningful changes, then push.

**Architecture:** Four independent read-only check tasks (one per pack) each write a delta report; a final sync task applies only deltas meeting the spec's "meaningful" bar via each pack's own method, then runs the validation suite and pushes. No product code is touched at any point.

**Tech Stack:** `opencode` CLI (plugin check/update), `git` (fresh clones to `/tmp`, never into the repo), `python3` (frontmatter/JSON validation sweeps from `docs/18-harness-packs.md`), `webfetch`-equivalent raw file fetches.

**Spec:** `docs/superpowers/specs/2026-11-08-harness-freshness-design.md` — the plan argues from the spec, so the spec travels with it; executors read both.

## Global Constraints

- pstack/ECC syncs are manual three-way merges, never verbatim overwrites; local OpenCode V2 rewrites and recorded skips (ECC V1 plugin, `/tdd` name, instinct system) are preserved.
- mattpocock re-vendors keep the MIT attribution lines and the `disable-model-invocation: true` flags on `grill-with-docs` and `improve-codebase-architecture`.
- Superpowers updates via the plugin CLI against project config only; no global `~/.config/opencode/` changes (repo-only rule).
- Validation suite from `docs/18-harness-packs.md` must pass before any commit: frontmatter sweep (every `.opencode/skills/*/SKILL.md` has `name` == directory, `description` 1–1024 chars), every `.opencode/commands/*.md` resolves, `opencode.json` parses, `opencode debug config` loads with no warnings.
- Model routing follows `.opencode/pstack-models.md` (recon on inkling, chores on beam-free).
- One logical change per commit; Conventional Commits via `.githooks`; `graphify-out/*` stays dirty and uncommitted; push to `development` only at the end.

## Review Focus

- Upstream unreachable or rate-limited mid-check — the task records the failure with the command and HTTP state and stops that pack's check instead of guessing; other packs proceed.
- Plugin update changes loaded skill behavior mid-session — record installed vs new version strings before and after; if behavior-relevant, flag in the report rather than silently accepting.
- Three-way merge conflict with a local V2 rewrite — resolve by keeping the local adaptation and noting it; never accept the upstream hunk blindly.
- Validation suite flags after sync — the sync task fixes or reverts the offending hunk; nothing commits red.
- mattpocock upstream restructured paths — report the new layout and stop; do not invent mappings.

---

### Task 1: Superpowers plugin delta check

**Files:**
- Read: `.opencode/opencode.json` (plugin pin)
- Write: `<workspace>/task-1-report.md` (installed vs latest versions, update-or-skip verdict)

**Interfaces:**
- Consumes: nothing (first task).
- Produces: `task-1-report.md` with `installed: <ver>`, `latest: <ver>`, verdict `update` | `skip + reason` — Task 5 reads the verdict.

- [ ] **Step 1: Record the installed plugin version**

Run: `opencode plugin list 2>&1 | head -10` (cwd repo root)
Expected: lists `superpowers` with a version or commit; copy the exact string into the report.

- [ ] **Step 2: Check for updates without applying**

Run: `opencode plugin check 2>&1 | head -20` (cwd repo root)
Expected: reports whether `superpowers` is behind; if the subcommand differs, run `opencode plugin --help` first and record the correct one — never guess flags.

- [ ] **Step 3: Write the verdict to `<workspace>/task-1-report.md`**

`update` only if behind on a version affecting OpenCode (note the changelog one-liner); else `skip + reason`. No repo files changed; no commit.

### Task 2: mattpocock skills delta check

**Files:**
- Read: the 10 local files under `.opencode/skills/{grill-with-docs,improve-codebase-architecture,grilling,domain-modeling,codebase-design}/`
- Write: `<workspace>/task-2-report.md` (per-file same/changed + staged re-vendor list)

**Interfaces:**
- Consumes: nothing.
- Produces: `task-2-report.md` with per-file `same` | `changed (one-line diff summary)` for all 10 files; Task 5 re-vendors only `changed` files, preserving MIT attribution and `disable-model-invocation` flags.

- [ ] **Step 1: Fetch the 10 upstream files**

Run: `curl -sL https://raw.githubusercontent.com/mattpocock/skills/refs/heads/main/skills/engineering/grill-with-docs/SKILL.md` (and the other 9: `improve-codebase-architecture/SKILL.md` + `HTML-REPORT.md`, `productivity/grilling/SKILL.md`, `engineering/domain-modeling/SKILL.md` + `GLOSSARY-FORMAT.md` + `ADR-FORMAT.md`, `engineering/codebase-design/SKILL.md` + `DEEPENING.md` + `DESIGN-IT-TWICE.md`)
Expected: all 10 fetch HTTP 200; any non-200 is recorded with its code and that file is marked `unknown`, not guessed.

- [ ] **Step 2: Diff each upstream file against the local copy ignoring our attribution lines**

Run: `diff <(grep -v '^Source: https://github.com/mattpocock' local) upstream` per file
Expected: per-file `same` or a one-line change summary; attribution-only diffs count as `same`.

- [ ] **Step 3: Write `<workspace>/task-2-report.md`**

Full 10-row table plus the staged re-vendor file list (empty list is a valid outcome). No repo files changed; no commit.

### Task 3: ECC delta check

**Files:**
- Read: `.opencode/commands/`, `.opencode/prompts/agents/`, `.opencode/instructions/INSTRUCTIONS.md`
- Write: `<workspace>/task-3-report.md` (changed-files list restricted to our installed subset)

**Interfaces:**
- Consumes: nothing.
- Produces: `task-3-report.md` with `changed: <paths>` (upstream→local relevant diffs only) or `no relevant changes`; Task 5 three-way-merges only listed files.

- [ ] **Step 1: Fresh-clone ECC to scratch (never into the repo)**

Run: `git clone --depth 50 https://github.com/affaan-m/ECC /tmp/ecc-upstream 2>&1 | tail -2`
Expected: clone succeeds; on rate-limit failure, retry once with `GITHUB_TOKEN`, then record failure and stop this task.

- [ ] **Step 2: Diff our installed subset against the clone**

Run: `diff -rq .opencode/commands /tmp/ecc-upstream/commands; diff -rq .opencode/prompts/agents /tmp/ecc-upstream/prompts/agents 2>/dev/null; diff .opencode/instructions/INSTRUCTIONS.md /tmp/ecc-upstream/instructions/* 2>/dev/null | head -30`
Expected: a concrete changed-file list; files we deliberately skipped (V1 plugin, instinct system, per `docs/18-harness-packs.md`) are marked `out of scope`, not diffs.

- [ ] **Step 3: Write `<workspace>/task-3-report.md`**

Changed list with one-line relevance each, or `no relevant changes`. No repo files changed; no commit.

### Task 4: pstack mirror delta check

**Files:**
- Read: `.opencode/SYNC-LOG.md` (last sync: mirror `f104aa8`, 0.15.36), `.opencode/skills/`, `.opencode/agents/`, `.opencode/commands/`, `.opencode/prompts/`
- Write: `<workspace>/task-4-report.md` (mirror commits since `f104aa8` touching our subset)

**Interfaces:**
- Consumes: nothing.
- Produces: `task-4-report.md` with `commits: <shas + one-liners>` and `files: <paths in our subset>`; Task 5 three-way-merges only listed files, preserving OpenCode-only divergences.

- [ ] **Step 1: Fetch the mirror log without cloning the repo**

Run: `git ls-remote https://github.com/backnotprop/pstack HEAD 2>&1`
Expected: upstream HEAD sha recorded; if unreachable, record and stop this task.

- [ ] **Step 2: List mirror commits since `f104aa8` touching our vendored areas**

Run: shallow fetch to scratch (`git clone --depth 100` to `/tmp/pstack-upstream` only if Step 1 shows movement, else record `no movement` and skip), then `git log --oneline f104aa8..HEAD -- skills/ agents/ commands/ prompts/ | head -30`
Expected: commit list or `no movement`; full-history archaeology is out of scope.

- [ ] **Step 3: Write `<workspace>/task-4-report.md`**

Commits + affected files in our subset, or `no movement`. No repo files changed; no commit.

### Task 5: Selective sync, validate, push

**Files:**
- Modify: only files staged by Tasks 1–4 reports and meeting the spec's "meaningful" bar
- Read: `docs/18-harness-packs.md` (validation suite), `.opencode/SYNC-LOG.md` (append entry)

**Interfaces:**
- Consumes: `task-1-report.md` … `task-4-report.md` verdicts (update/skip, staged file lists, no-movement notes).
- Produces: green validation run, `chore(harness)` commit(s), push of `development`; `SYNC-LOG.md` entry with source versions.

- [ ] **Step 1: Apply only meaningful deltas, pack by pack**

Superpowers `update` verdict → `opencode plugin update`; mattpocock `changed` files → re-fetch and write, preserving attribution + flags; ECC/pstack files → manual three-way merge keeping local V2 rewrites. Pure other-harness wording and out-of-scope files are skipped with a reason in the commit body or report.

- [ ] **Step 2: Run the validation suite**

Run: `python3` frontmatter sweep + `python3 -c "import json; json.load(open('.opencode/opencode.json'))"` + `opencode debug config` (cwd repo root)
Expected: sweep clean, JSON valid, config loads with no warnings; fix or revert offenders — nothing commits red.

- [ ] **Step 3: Append `SYNC-LOG.md`, commit, push**

Run: `git add` (only intended files — verify with `git status --short`), `git commit -m "chore(harness): sync <packs> to <versions>"`, `git push origin development`
Expected: hook passes, push succeeds; report versions per pack.
