# LinkedIn parser fixtures

Synthetic HTML for the offline LinkedIn parsers
(`JobScrape.Infrastructure/Sources/LinkedIn`). The class names, attributes,
and nesting reproduce live linkedin.com guest markup; every company, title,
id, and URL is invented. Real captured pages are never committed here.

| Item | Value |
|---|---|
| Live markup modelled | 2026-11-02 (linkedin.com guest search) |
| Selector version | 1 |
| Search URL shape | `https://www.linkedin.com/jobs/search?keywords=&location=&f_TPR=r604800&start=` (`start` in steps of 25) |
| Detail URL shape | `https://www.linkedin.com/jobs/view/{id}` (guest detail is unsupported; the adapter never fetches it) |

## Selectors (version 1)

- Card: `li.jobs-search-results__list-item`; fallback `li:has(a[href*="/jobs/view/"])`.
- Title/link: `a.job-card-container__link` (`/jobs/view/{numericId}`).
- Company `span.job-card-container__company-name`; location
  `li.job-card-container__metadata-item`; posted `time`.
- Result count `.jobs-search-results-list__subtitle`; Next is an `a` to the
  same search with a larger `start`.

## Files

| Scenario | File |
|---|---|
| `search-guest.html` | 6 public guest cards with canonical `/jobs/view/{id}` links. |
| `search-authwall.html` | Guest wall: sign-in title and join-to-view body, zero cards. |
| `search-429.html` | Rate-limit page, zero cards (classified by the 429 status). |
| `search-empty.html` | Legitimate zero results (with a reCAPTCHA footer notice). |
