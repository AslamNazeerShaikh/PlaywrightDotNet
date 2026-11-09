# New-only scrape runs — design

Intent: a per-run choice on the runs start form, "only new jobs", for
operators who re-scrape a profile on a schedule and want each run to
surface postings never seen before. The operator's example is a 7-day
Naukri scrape where the results list only fresh postings while the
database stays current.

Why: repeated runs today re-list every refreshed posting in the run
window, so the operator pages through known jobs to find fresh ones.
The run's stats already count new vs updated, but nothing lets a run
collect and display only the new set.

## Goals

- A new-only run stores fresh postings and shows only those as its
  results, in the postings list and in CSV/XLSX exports.
- Already-stored postings sighted again still refresh `LastSeenAt`
  and content exactly as today. No row is duplicated and no stored
  row goes stale.
- Already-seen cards do not consume the run's job cap in a new-only
  run, so paging continues until the cap fills with fresh postings
  or pages run out.
- Normal runs behave byte-for-byte as today.

## Non-goals

- Fuzzy repost matching. A reposted job under a new external id
  looks new. Documented as a limit, not solved.
- Changing normal-run listing, stats, export, or cap semantics.
- Backfilling the flag on runs recorded before this change. They
  read as normal runs, which matches how they executed.
- `Idempotency-Key`, SSE, or any other item in the cockpit doc's
  not-implemented list.

## Anti-goals

- A parallel status model next to `RunStage`, or a second listing
  query path. One flag on the run, one predicate at the read edge.
- A display-only filter that leaves ingest consuming the job cap
  on known rows. That would show new jobs without taking only new
  ones, which is the exact disappointment being fixed.

## Constraints

- Architecture rules from `AGENTS.md`: explicit constructor-injected
  handlers, `Result<T>` for expected failures, validated options at
  the composition root, RFC 9457 problems at the edge. No mediator,
  no generic repositories.
- `ScrapeRun` is the aggregate: lifecycle, stats, and the mode flag
  live there. `PageStats`/`RunStats` keep their meaning
  (found = valid postings parsed, new = stored first time).
- EF Core migrations are the schema path. Test databases migrate
  on creation (`TestDatabase` calls `MigrateAsync`), so a new
  migration covers tests and dev databases alike.
- Typecheck gates: `bun check`, `dotnet build` with 0 warnings.
- TDD for every behavior change. Full suites plus e2e before any
  push. Review loop must be clean before merge.

## Approaches

Two real options were considered.

**Picked: the run remembers it was new-only (approach 1).** The
checkbox travels as a boolean from the start form through
`StartRunRequest`, `StartRunHandler`, and `RunScrapeRequest`, is
stored on `ScrapeRun`, exempts known rows from the job cap during
the scrape, and filters the postings list and exports to postings
first seen inside the run window. Costs more files, matches the
scenario end to end.

**Dropped: display-only filtering (approach 2).** Same checkbox,
but ingest keeps consuming the job cap on known rows and stats
mix new with updated. Smaller diff, but a run can burn its cap
refreshing known rows and finish with almost nothing fresh.

**Ruled out earlier:** skipping the refresh of known rows (the
operator rejected it; the database must stay current), and fuzzy
repost detection (out of scope).

## What gets built

Backend, in dependency order:

- `RunScrapeRequest` and `StartRunRequest` gain
  `bool OnlyNew = false`. `StartRunHandler` passes it through.
  `RunsController` keeps its signature; its doc comment names the
  new body field.
- `ScrapeRun` gains a get-only `OnlyNew`, set through
  `Create(profile, now, onlyNew: false)`. The optional parameter
  keeps every existing caller compiling. The mode is snapshot at
  creation like limits and companies.
- `ScrapeRunConfiguration` maps the column; a new migration
  `AddOnlyNewToScrapeRuns` plus the model snapshot carry it.
  Old rows read `false`.
- `RunScope` gains `OnlyNew` from the request, next to `Enrich`.
  `SourceRunContext` is untouched: adapters never see the mode.
- `SearchAsync` computes the job budget from `PostingsNew` when
  the run is new-only, `PostingsFound` otherwise, for both the
  loop-stop check and the `remaining` passed to `UpsertAsync`.
  Counter semantics do not change: `PostingsFound` still counts
  valid postings parsed, so in a new-only run it can exceed
  `MaxJobs`; the cap binds `PostingsNew` there. This is the one
  place the cap's meaning is mode-dependent, and it is documented
  on the loop, not in the stats types.
- `UpsertAsync` is otherwise unchanged: known rows still
  `FindAsync` then `Refresh`, still join `Touched`, still count
  into `PageStats` with today's meaning. The only difference is
  the per-page budget break, which counts added postings in
  new-only mode so a page of known rows does not consume the
  page's budget. `remaining` arrives as
  `MaxJobs - PostingsNew` in new-only mode.
- `PendingEnrichment` orders fresh-first in new-only mode: rows
  added by this run come before refreshed known rows without a
  description, discovery order within each group. Without this,
  the detail cap could burn on known rows while fresh rows stay
  unenriched, which is the same disappointment the job-cap rule
  fixes, moved to the detail cap.
- No CLI surface exists in this repo (no CLI project; the
  `--no-enrich` mention in `RunScrapeRequest` is a stale
  reference to a planned surface), so there is no CLI flag to
  wire. Out of scope; not fixed here.
- `RunResponse` and `ScrapeRunSummary` both gain `OnlyNew` with
  their `From` mappings, so polling and the handler's own return
  agree on the mode.
- `RunReadStore.PostingsAsync` adds `FirstSeenAt >= from` when
  `run.OnlyNew` is true, with `from` the same run-start instant
  already used for the `LastSeenAt` lower bound, inclusive on
  both sides. List and export share this path, so both are
  covered. The `isNew` mark (`FirstSeenAt >= runStart`) is
  reused logic, now also the filter. The `StartedAt is null`
  early return already yields an empty list for unstarted runs.
- MCP `start_run` gains `onlyNew = false` and passes it through.

Frontend:

- `api.ts`: `StartRunInput` and `RunPayload` gain
  `onlyNew: boolean`.
- `runs.tsx`: a `NEW ONLY` checkbox next to the enrich checkbox
  (`name="onlyNew"`, unchecked by default), sent as
  `onlyNew: data.get("onlyNew") !== null`. The run detail shows a
  NEW-ONLY badge when `selected.onlyNew` is true, and the postings
  module notes the list holds postings first seen in this run.
- Regenerate OpenAPI contract types (`bun run api:types`) if
  `shared/contracts.ts` carries the run shapes.

Tests, RED first in each layer:

- Domain: `Create` defaults `OnlyNew` false, preserves true.
- Application (`ScrapeHarness` scripted pages with repeats):
  new-only counts only fresh toward the cap and keeps paging,
  refreshed rows update `LastSeenAt` without duplicating, stats
  keep their meaning, normal-run behavior unchanged.
- `StartRunHandler`: the flag passes through to the launcher.
- Infrastructure SQLite: the flag persists; a flagged run's
  listing and export hold only in-window first-seen postings;
  an unflagged run lists as today.
- API: `POST /api/v1/runs` accepts `onlyNew`, absent means false.
- Frontend `bun test`: `startRun` sends the flag, the form renders
  the checkbox default-off, the detail badges a flagged run.
- E2E: extend `runs.e2e.ts` with a two-run scenario against the
  temp backend — second run flagged shows only fresh postings.
  If the temp-backend seeding cannot produce repeats cleanly,
  the builder proves that in the spike and scopes e2e to the
  API-level temp-backend harness instead.

Docs:

- `docs/03-domain-model.md` §8.5: the new-only rule.
- `docs/12-web-cockpit.md` runs section: the body field, the
  listing filter, the badge.
- `src/frontend/README.md` runs paragraph if it names the start
  options. `docs/09-operations-runbook.md` if it describes reading
  a run. The builder greps both and updates what names the old
  behavior.
- Commit message follows Conventional Commits
  (`feat(runs): ...`), one logical change, docs in the same
  commit only if the repo practice allows; otherwise a stacked
  `docs(runs): ...` commit.

## Added scope: website diagrams, info tooltips, visited links

The operator added three items. They ride this change.

### Website diagrams

- The two diagrams live on the website, not as standalone files.
  Home is the Topology page: two modules, one for the current
  scrape flow, one for the new-only delta. They reuse
  `components/diagram.tsx` tones, `DiagramDefs` markers, and the
  `Legend` component, and read deck CSS variables so light and
  dark themes stay in sync.
- Layout follows the diagram-design flowchart grammar:
  orthogonal connectors, short labels, one accent (emerald) for
  the focal path, gray for dropped-card exits, aria-labelled
  `<svg role="img">` like the existing TOP-01 diagram.
- Diagram 1 (current flow): search loop, per-card gates, new vs
  refresh write, stats, enrichment, listing window. Max 9 nodes.
- Diagram 2 (new-only): the same path with the three
  divergences marked: job budget from `PostingsNew`, per-page
  budget counting added rows, listing and export filtered to
  first-seen in window.
- Tests: `bun test` render tests (modules present, aria labels,
  tone of the new-only path); the e2e topology pass covers the
  route. No skill file scripts run for these; the repo harness
  is the verification.

### Info tooltips

- New reusable `InfoTip` component: a circled `i`, hover and
  focus tooltip, keyboard reachable, `aria-describedby`,
  silent under `prefers-reduced-motion`. One component, one
  style, used wherever a usage explanation sits.
- This change places it on the runs page: the NEW ONLY and
  enrich checkboxes, the NEW-ONLY badge, the run stat readouts,
  and the postings module note. Other pages keep their current
  `Abbr` usage; adopting `InfoTip` page by page is a follow-up,
  not this change.
- Tests: `bun test` for render, text, and accessibility wiring;
  e2e asserts a tooltip appears on hover and focus of the NEW
  ONLY control.

### Visited posting links

- Posting title links in data tables turn violet after a visit:
  dark `#a78bfa`, light `#6d28d9` (the deck `brand-3`), via a
  native `:visited` rule on `.data a`. Color-only, which is all
  browsers expose, and no history tracking is added.
- Tests: browsers hide visited state from scripts, so no
  automated color assertion is possible. The builder proves the
  rule ships (stylesheet assertion or computed-style check on
  the unvisited color) and verifies the visited color by eye in
  both themes; the e2e run-postings pass keeps covering the
  links themselves.

## Left to the builder

- Exact migration name and column mapping style follow the
  existing `InitialCreate` files.
- E2E seam choice per the constraint above.
- Whether `shared/contracts.ts` is committed and needs regen.
