# Conventional Commits

> **Purpose:** define how every commit message in this repository is written, reviewed, and validated.
>
> **Standard:** [Conventional Commits v1.0.0](https://www.conventionalcommits.org/en/v1.0.0/#specification). This document summarizes the specification and adds the project rules that make it enforceable. The specification is authoritative if the two disagree.

---

## 1. Format

```text
<type>[optional scope][!]: <description>

[optional body]

[optional footer(s)]
```

A real example:

```text
feat(sources/naukri): extract JSON-LD job postings

Adds a parser for the JSON-LD block and falls back to DOM selectors when the
block is absent. The fixture covers both paths.

Refs: #42
```

### 1.1 Header

| Part | Rule |
|---|---|
| `type` | A noun describing the change. Lowercase in this project. |
| `(scope)` | Optional noun in parentheses. Lowercase in this project. Prefer the narrowest area. |

> The specification requires types and scopes to be treated case-insensitively. The validator accepts `Feat`, `FEAT`, and `feat`; the lowercase forms above are a project style choice, not a validity rule.
| `!` | Optional. Marks a breaking change. Placed immediately before the colon. |
| `: ` | Required colon followed by one space. |
| `description` | Required short summary, immediately after the colon and space. |

Examples:

```text
feat: add CSV export
fix(cli): return exit code 2 on validation failure
docs(adr): record the SQLite WAL decision
feat(api)!: replace run status strings with a typed object
```

### 1.2 Body

- Optional.
- **Must** begin one blank line after the description.
- Free-form; explain **why** and any context a reviewer needs, not what the diff already shows.
- Separate paragraphs with blank lines.

### 1.3 Footers

- Optional, one blank line after the body (or after the description when there is no body).
- Each footer is a token, then `: ` or ` #`, then a value: `Refs: #42`, `Reviewed-by: Aslam`.
- Tokens use `-` instead of spaces: `Co-authored-by`, `Acked-by`.
- `BREAKING CHANGE` is the one token that may contain a space. It must be uppercase.
- `BREAKING-CHANGE` is accepted as a synonym.

Common footers in this project:

```text
Refs: #42
Closes: #42
Co-authored-by: Name <email@example.com>
BREAKING CHANGE: `GET /api/v1/runs/{id}` now returns `status` as an object.
```

### 1.4 Breaking changes

A breaking change **must** be indicated in one of two ways:

1. A `!` immediately before the colon:

   ```text
   feat(api)!: replace run status strings with a typed object
   ```

2. A `BREAKING CHANGE:` footer:

   ```text
   feat(api): replace run status strings with a typed object

   BREAKING CHANGE: `GET /api/v1/runs/{id}` now returns `status` as an object.
   ```

Both may be used together. When `!` is used, the description must explain the break; the footer is optional.

---

## 2. Types used in this repository

The specification requires `feat` and `fix` and allows other types. This project accepts:

| Type | Use for | Version effect* |
|---|---|---|
| `feat` | A new user-visible capability | MINOR |
| `fix` | A bug fix | PATCH |
| `perf` | A performance improvement with no intended behavior change | PATCH |
| `refactor` | A behavior-preserving restructure | none |
| `test` | Tests only | none |
| `docs` | Documentation only | none |
| `build` | Build system, project files, dependencies, packaging, hooks | none |
| `ci` | CI configuration and scripts | none |
| `style` | Formatting or whitespace only | none |
| `chore` | Maintenance that does not fit the types above | none |
| `revert` | Reverting an earlier commit | none |

\* Only relevant once the project starts tagging releases. A `!` or `BREAKING CHANGE` footer means MAJOR.

Rules of thumb:

- If a change adds or changes behavior a user or adapter can observe, it is `feat` or `fix`, not `chore`.
- If a commit only touches tests, use `test`, even when it fixes a broken test.
- If a commit only updates documentation, use `docs`, even when the documentation describes a feature.
- Do not use `feat` for internal scaffolding that no caller can use yet. Use `refactor`, `build`, or `chore`.

### 2.1 Scopes

Scopes are optional. Prefer one of these, or the nearest package/module name:

```text
domain, app, infra, cli, api, cockpit, sources, naukri, indeed, linkedin,
compliance, storage, exports, docs, build, ci, deps, graph
```

Use a slash for a sub-area when it improves clarity: `sources/naukri`, `sources/indeed`.

The validator accepts scopes case-insensitively, as the specification requires, and allows only letters, digits, `.`, `_`, `/`, and `-` after lowercasing.

---

## 3. Project rules

These are stricter than the specification and are enforced by review:

1. **One logical change per commit.** Tests belong in the same commit as the behavior they protect. Documentation may be split into its own commit when it is a standalone change.
2. **Imperative mood, lowercase description.** Write `add quota policy`, not `added` or `Adds`.
3. **No trailing period** in the description.
4. **Header target: 72 characters; hard maximum: 100.** Move detail into the body.
5. **Explain why in the body.** The diff explains what changed.
6. **Reference issues in footers**, not in the description: `Refs: #42`, `Closes: #42`.
7. **Never commit secrets, local databases, browser profiles, or generated artifacts.** See `.gitignore`.
8. **PR titles follow the same format** when the merge strategy squashes commits, because the PR title becomes the commit header.
9. **Merge commits, Git reverts, and rebase fixups are exempt.** The hook allows `Merge ...`, `Revert "..."`, `fixup!`, `squash!`, and `amend!`.

### 3.1 Examples

```text
feat(domain): add job posting deduplication policy
fix(sources/naukri): handle an empty JSON-LD block
perf(storage): add a covering index for last-seen queries
refactor(infra): extract session bootstrap from the adapter
test(app): cover quota exhaustion before the first page fetch
docs(adr): record the React and Bun cockpit decision
build(deps): bump Playwright to 1.64.1
ci: run architecture tests on pull requests
chore(graph): rebuild the directed knowledge graph
revert: feat(api): add run cancellation
```

A revert created by Git keeps its generated header and is allowed by the hook:

```text
Revert "feat(api): add run cancellation"

This reverts commit 1a2b3c4d5e6f.
```

---

## 4. Enforcement

The repository ships a dependency-free POSIX `sh` validator at `.githooks/commit-msg`. It runs before every commit and rejects:

- a missing or unknown type;
- a malformed scope or missing `: ` separator;
- an empty description;
- a header longer than 100 characters;
- a body that is not separated from the header by a blank line.

It does **not** try to validate prose, footer semantics, or issue numbers.

Enable it once per clone:

```bash
git config core.hooksPath .githooks
```

Check a single message without committing:

```bash
printf 'feat(domain): add quota policy\n' > /tmp/commit-msg
.githooks/commit-msg /tmp/commit-msg
```

Check every commit on the current branch that is not on `main`:

```bash
tmp=$(mktemp)
git log --format=%H main..HEAD | while read -r sha; do
  git log -1 --format=%B "$sha" > "$tmp"
  .githooks/commit-msg "$tmp" || echo "invalid commit: $sha"
done
rm -f "$tmp"
```

The CI pipeline will run the same loop against the pull request's commit range, so messages pushed through other tools cannot skip validation.

Bypass only for a Git-generated message the hook cannot classify:

```bash
git commit --no-verify
```

Do not use `--no-verify` to avoid fixing a real message. If a legitimate message is rejected, fix the hook in the same change.

> **Status:** the hook is local. A CI commit-message check will be added with the Phase 0 pipeline so commits pushed through other tools cannot skip it.

---

## 5. Relationship to versioning

Conventional Commits is not a SemVer replacement. It gives release tooling a reliable signal:

- `fix` → PATCH
- `feat` → MINOR
- `!` or `BREAKING CHANGE` → MAJOR

The project uses MinVer for version derivation. Commit types are not a promise that every `fix` ships immediately; they describe the change, not the release schedule.

---

## 6. References

- [Conventional Commits v1.0.0 specification](https://www.conventionalcommits.org/en/v1.0.0/#specification)
- [Conventional Commits FAQ](https://www.conventionalcommits.org/en/v1.0.0/#faq)
- [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html)
