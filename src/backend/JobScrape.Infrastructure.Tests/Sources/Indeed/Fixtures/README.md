# Indeed parser fixtures

Synthetic HTML for the offline Indeed parsers
(`JobScrape.Infrastructure/Sources/Indeed`). The class names, attributes,
and nesting reproduce live in.indeed.com markup; every company, title, id,
and URL is invented. Real captured pages are never committed here.

| Item | Value |
|---|---|
| Live markup modelled | 2026-11-02 (in.indeed.com guest search) |
| Selector version | 1 |
| Search URL shape | `https://in.indeed.com/jobs?q=&l=&fromage=&start=` (`start` in steps of 10) |
| Detail URL shape | `https://in.indeed.com/viewjob?jk={key}` |

## Selectors (version 1)

- Card: `div.job_seen_beacon[data-jk]`; fallback `div[data-testid="slider_item"]`.
- Title/link: `h2.jobTitle a` (absolute or root-relative `/viewjob?jk=…`).
- Company `span[data-testid="company-name"]`; location
  `div[data-testid="text-location"]`; salary
  `div[data-testid="attribute_snippet_testid"]`; snippet
  `div[data-testid="jobsnippet_footer"]`; posted `span.date`.
- Result count `div.jobsearch-JobCountAndSortPane-jobCount`; Next is an `a`
  whose `href` carries a larger `start` (or whose text is `Next`).
- Detail: `script[type="application/ld+json"]` `JobPosting` first; DOM
  fallbacks `h1.jobsearch-JobInfoHeader-title`,
  `[data-testid="inlineHeader-companyName"]`, `[data-testid="job-location"]`,
  `#jobDescriptionText`, `#applyButtonLinkContainer a`.

## Files

| File | Scenario |
|---|---|
| `search-normal.html` | 20 cards, count `1–10 of 200 jobs`, Next to `start=10`. |
| `search-empty.html` | Legitimate zero results (with a reCAPTCHA footer notice). |
| `search-cloudflare.html` | Cloudflare managed-challenge block page. |
| `detail-normal.html` | JSON-LD + header + description + salary + company-site apply link. |
| `detail-minimal.html` | No JSON-LD; title, location, and description only. |
