# Naukri parser fixtures

Synthetic HTML for the offline Naukri parsers
(`JobScrape.Infrastructure/Sources/Naukri`). The class names, attributes,
and nesting reproduce live naukri.com markup; every company, title, id, and
URL is invented. Real captured pages are never committed here.

| Item | Value |
|---|---|
| Live markup captured | 2026-11-01 (headed Chrome, naukri.com) |
| Selector version | 1 |
| Search URL shape | `https://www.naukri.com/{slug(keywords)}-jobs-in-{slug(firstCity)}?k=&l=&experience=&jobAge=` (page 1; later pages via native **Next**) |
| Detail URL shape | `https://www.naukri.com/job-listings-{slug}-{numericId}` |

## Selectors (version 1)

- Card: `div.srp-jobtuple-wrapper[data-job-id]` > `div.cust-job-tuple`.
- Title/link: `a.title` (absolute `https://www.naukri.com/job-listings-…-{id}`).
- Company `a.comp-name`; experience `span.expwdth`; salary `.sal-wrap span[title]`;
  location `span.locWdth`; snippet `span.job-desc`; skills `ul.tags-gt li.tag-li`;
  posted `span.job-post-day`.
- Result range `span[class*="count-string"]`; Next is an `a` whose text is `Next`
  with `aria-disabled="false"` (`"true"` on the last page).
- Promoted widgets (`div.srp-job-promotion`, `#urgent-hiring-widget`, `acp-job-promotion`)
  repeat listed jobs and are not parsed.
- Detail: `script[type="application/ld+json"]` `JobPosting` first; DOM fallbacks
  `[class*="jd-header-title"]`, `[class*="jd-header-comp-name"] a`,
  `[class*="jhc__exp__"] span`, `[class*="jhc__salary"] span`, `[class*="jhc__loc"]`,
  `[class*="jhc__stat"]` (Posted), `[class*="JDC__dang-inner-html"]`, `[class*="key-skill"] a`.

## Files

| File | Scenario |
|---|---|
| `search-normal.html` | 20 organic cards, range `1 - 20 of 131`, Next enabled. |
| `search-promoted.html` | 3 organic cards plus promoted widgets that repeat them. |
| `search-honeypot.html` | 2 visible cards plus hidden traps and cards with off-host / `javascript:` / hidden links. |
| `search-empty.html` | Legitimate zero-result search (with a reCAPTCHA footer notice). |
| `search-challenge.html` | Akamai-style `Access Denied` block page. |
| `search-last-page.html` | Last page, range `121 - 131 of 131`, Next `aria-disabled="true"`. |
| `detail-normal.html` | JSON-LD + header + description + skills + external company-site apply link. |
| `detail-minimal.html` | No JSON-LD; title, description, and skills only. |

The search fixtures are generated from a small script kept outside the repo;
edit them by hand when the live markup drifts, bump the selector version, and
update the capture date.
