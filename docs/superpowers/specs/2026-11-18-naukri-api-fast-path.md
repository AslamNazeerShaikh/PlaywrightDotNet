# Spec — Naukri API fast path (default on, browser off)

Status: design locked 2026-11-18. Implements the JobSpy-port approach proven
in `AslamNazeerShaikh/PythonWebScraping` (`src/web/nkapi.py`, MIT JobSpy
constants) against this repo's source-adapter and challenge contracts.

## Goal

Naukri runs fetch listings through Naukri's own `jobapi` search/detail
endpoints by default. The Playwright browser path stays available behind an
explicit opt-out for when the API path is walled.

## Contract

- Search: `GET https://www.naukri.com/jobapi/v3/search`, headers
  `appid: 109`, `systemid: Naukri`, fresh `nkparam` per request.
- Detail: `GET https://www.naukri.com/jobapi/v4/job/{id}`, headers
  `appid: 121`, `systemid: Naukri`, fresh `nkparam(jobId)` per request.
- Token: RSA PKCS#1 v1.5 of `v0|<epoch-ms>|121_<page>` (`srp` for search)
  under Naukri's published public key (JobSpy `constant.py`, MIT —
  attribute it in the comment). Tokens are time-bound and never replayed.
- Paging: `noOfResults=20`, `pageNo=N`. Stop at `max_jobs`, empty page,
  `pageNo × 20 ≥ noOfJobs`, or cancel. `400` past the last page is a clean
  end. Dedupe by `jobId` across pages.
- Freshness-first params: `jobAge` + `sort=f`, keyword/location/experience
  as plain params (JobSpy's shape).
- Row mapping reuses the existing parser gate: `tagsAndSkills`,
  `companyName`, salary, and placeholders work mode map onto the same posting
  shape browser cards produce. `ambitionBoxData` ratings are not mapped on
  either path (parity; enrichment out of scope). Bad rows drop with a
  warning, never poison a run.
- Client posture: honest Chrome UA sent as-given, `Accept: application/json`,
  15–30 s timeout, auto-redirects OFF, 3–7 s jittered pause between pages
  via the existing `IDelay`/`IRandomSource` and `Min/MaxDelayMs` options.

## Transport selection

- New per-source `SourceOptions.ApiOnly` bool, default `true`.
- `true` (default): runs use the API implementation; the browser is never
  launched for Naukri.
- `false`: today's Playwright path runs byte-identical (existing suite pins it).
- A second `IJobSource` (`NaukriApiJobSource`) behind the same `SourceKey`;
  the registry prefers it when the flag is on. Browser code is untouched.

## Failure mapping (no fallback when API-only)

- `200` → parse rows through the shared gate.
- `400` → clean end of listing.
- `406` → `RateLimited` wall, run ends with partials kept. Three
  consecutive `406`s name key rotation as the likely cause.
- Any other non-200, timeout, or network failure → `TransientError` wall.
- Unexpected redirects are classified as walls, never followed.

## Protection rules (policy-bound)

- No TLS impersonation, no proxy rotation, no harvested cookies, no login.
  Both are banned by the repo's anti-bot rule; JobSpy leans on them, we
  explicitly do not. Politeness (caps, delays, cooldowns, honest UA, fresh
  tokens) is the whole defense, proven sufficient at our volume (444 API vs
  435 browser postings, skills agreeing 14/15 in the reference runs).
- `MinimumFreeDiskBytes`, daily caps, and challenge cool-downs apply to API
  runs exactly as they do to browser runs.

## Decisions and rulings

- Per-source `ApiOnly` (default true), not a sixth global feature flag:
  transport is a per-source choice; globals gate whole features.
- Separate `NaukriApiJobSource`, not a branch in `NaukriJobSource`: keeps
  browser code reviewable and untouched; shared parsers/classifier carry
  the common behavior.
- Hand-rolled `HttpClient` via `IHttpClientFactory` (`Microsoft.Extensions.Http`
  is already referenced), manual retry through `IDelay`. No Refit, no Polly
  package: two endpoints don't earn a dependency, and the test style already
  fakes `HttpMessageHandler` well.
- Zero new packages. `System.Security.Cryptography` (token) and
  `System.Text.Json` (rows) are built-in.
- Pool stays registered: Indeed/LinkedIn adapters still use it. Dead-code
  removal is a later commit, not this spec.

## Test strategy

TDD throughout. Token minter tested by properties (size, freshness, format).
Client tested with fixture JSON and a fake handler: never a live call, never
a browser. Flag-off browser suite must stay green unchanged. Coverage gate
from the repo plan still holds; new code is the most testable kind (pure
mapping + fake-transport branches).

## Non-goals

- Indeed/LinkedIn API paths. Indeed JSON-LD stays as-is.
- Internship job type (domain has none).
- Skills-scan aggregation (a later consumer of the same rows).
- Scheduler, MCP tools, cockpit changes beyond the existing run views.
