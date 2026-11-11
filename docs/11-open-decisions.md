# Open Decisions

> **Purpose:** unblock Phase 0. Each decision has a recommended default; if you do not choose, the recommendation is used.
>
> **Deadline:** before the first commit of Phase 0 (target 2026-11-02).

---

## Decision summary

| # | Decision | Options | Recommendation | Why it matters |
|---|---|---|---|---|
| D1 | Project name | `JobLens`, `JobRadar`, other | **JobLens** (working name) | Namespaces, CLI, docs; renaming later is mechanical but noisy. |
| D2 | LinkedIn | Disable in v1 / opt-in high-risk / remove | **Opt-in high-risk, default disabled** | Legal/ToS risk; adapter isolated so it can be removed. |
| D3 | Indeed | Enable by default / opt-in / defer | **Opt-in, conservative caps** | Cloudflare makes default-on a poor first experience. |
| D4 | Data fields | Minimal / full | **Full posting fields** (title, company, location, experience, salary, posted, skills, description, apply URL) | Affects schema, parsers, exports. |
| D5 | Output formats | CSV / Excel / JSON / Postgres | **CSV + Excel** in v1; JSON optional; Postgres later | Keeps v1 small; Excel is the primary human workflow. |
| D6 | Scheduling | OS scheduler / in-process daemon | **OS scheduler** | Simpler, reliable, no daemon; cron/launchd/Task Scheduler. |
| D7 | API/dashboard | v1 / v2 / never | **v2 (after Phase 3)** | Avoids building UI before the pipeline is proven. |
| D8 | Egress | Normal network / static proxy / rotation | **Normal network**; optional user-supplied static egress | Rotation is out of policy; static helps geo consistency. |
| D9 | Browser default | Bundled Chromium / Firefox / WebKit | **Bundled Chromium** only | Lowest install friction; pinned bundled build. |
| D10 | Recruiter emails | Include / defer | **Defer (out of scope)** | Personal-data/privacy risk; v1 is job postings only. |
| D11 | License | MIT / Apache-2.0 / private | **Private or MIT** | Affects sharing/contributions; decide before publishing. |
| D12 | Data retention | 30/90/180 days | **Artifacts 30d, logs 90d, postings 180d** | Privacy/storage balance; configurable. |

---

## D1 — Project name

**Question:** keep `JobLens` or choose another name?

| Option | Pros | Cons |
|---|---|---|
| **JobLens** | Short, descriptive, easy namespace `JobLens.*` | May collide with existing products; check before publishing. |
| JobRadar | Evocative of monitoring | More collisions likely. |
| Private/internal name | No branding work | Less polished for a public repo. |

**Recommendation:** use `JobLens` as a working name now; do a trademark/package/repo search before a public release. Renaming is a mechanical find/replace across namespaces, project names, and docs.

**Decision:** ____________________

---

## D2 — LinkedIn

**Question:** do we enable LinkedIn in v1?

**Facts:**

- LinkedIn's robots.txt says automated access without permission is strictly prohibited.
- The User Agreement prohibits scraping/crawling/automated collection.
- Auth wall/429/999 appear quickly; login automation increases account risk.
- There is no general public jobs API for this use case.

| Option | What it means | Risk |
|---|---|---|
| **A. Opt-in high-risk (recommended)** | Adapter exists, default disabled, explicit acknowledgment + interactive confirmation, guest-only, 1 page, stop on wall. | Medium–high; legal/account risk remains. |
| B. Disable/remove | Ship Naukri + Indeed only; document manual/official LinkedIn export. | Low; less coverage. |
| C. Enable by default | Convenient. | Unacceptable; do not do this. |

**Recommendation:** A, with a hard gate and a recommendation to use official/manual export. If your legal/compliance comfort is low, choose B; the architecture does not depend on LinkedIn.

**Decision:** ____________________

---

## D3 — Indeed

**Question:** enable Indeed by default or opt-in?

**Facts:** Cloudflare managed challenge + proprietary layer; blocks can appear within a handful of requests; headless often challenged; public pages exist but no general API.

| Option | Pros | Cons |
|---|---|---|
| **Opt-in (recommended)** | No surprise blocks on first run; explicit risk acceptance. | One extra config step. |
| Default on | More coverage out of the box. | Poor first-run experience; higher block risk. |
| Defer | Least risk. | Missing a major source. |

**Recommendation:** opt-in with the conservative caps in the playbook (2 pages, 40 jobs, 12–25s delay, 1 run/day).

**Decision:** ____________________

---

## D4 — Data fields

**Question:** which fields must v1 collect?

**Recommended set:**

- Source, external job ID, canonical URL, apply URL.
- Title, company, company URL (optional).
- Location raw + normalized city/region/country, work mode.
- Experience raw (and parsed min/max if unambiguous).
- Salary raw (and parsed min/max/currency/period if unambiguous).
- Posted date raw + parsed date.
- Description (capped), snippet, skills.
- First/last seen, content hash, status, data-quality flags.

**Not in v1:** recruiter names/emails/phones, profiles, application answers, interview reviews.

**Decision:** ____________________

---

## D5 — Output formats

**Question:** CSV + Excel enough?

| Option | Pros | Cons |
|---|---|---|
| **CSV + Excel (recommended)** | Universal; Excel hyperlinks are the human workflow. | No nested JSON. |
| Add JSON | Machine-friendly; easy for agents. | Extra writer/tests. |
| Add Parquet | Analytics-friendly. | Extra dependency; overkill for personal use. |
| Postgres | Multi-user/concurrency. | Ops overhead; not needed. |

**Recommendation:** CSV + Excel in v1; JSON export is a small optional add in Phase 5. Keep the export port so Parquet/Postgres can be added later.

**Decision:** ____________________

---

## D6 — Scheduling

**Question:** OS scheduler or in-process daemon?

| Option | Pros | Cons |
|---|---|---|
| **OS scheduler (recommended)** | Simple, reliable, survives app crashes; no daemon. | Platform-specific examples; no built-in UI. |
| In-process daemon | Cross-platform config; live dashboard possible. | Long-running process; locking/health/restart concerns. |
| Both | Flexible. | More code/test surface. |

**Recommendation:** OS scheduler for v1. Add a hosted scheduler only if a dashboard/API needs it.

**Decision:** ____________________

---

## D7 — API/dashboard

**Question:** include an ASP.NET Core API/dashboard in v1?

| Option | Pros | Cons |
|---|---|---|
| **v2 (recommended)** | Focus on the pipeline; CLI is enough for one user. | No browser UI. |
| v1 read-only API | Easy queries from other tools. | Extra surface/tests/docs. |
| v1 dashboard | Nice UX. | Scope creep; frontend work. |

**Recommendation:** after Phase 3, add read-only endpoints for jobs/runs if needed. Do not move scraping into controllers.

> **v2 note:** the API is now the prerequisite for the local React/Bun cockpit, so it is the first v2.0 deliverable. v1 remains CLI-only. See [Web Cockpit](12-web-cockpit.md) and [ADR-0010](10-adr/0010-react-bun-cockpit.md).

**Decision:** ____________________

---

## D8 — Egress / proxy

**Question:** normal network, static proxy, or rotation?

| Option | Pros | Cons |
|---|---|---|
| **Normal network (recommended)** | Lowest legal/detection risk; no setup. | Geo must match profile locale/timezone. |
| User-supplied static residential proxy | Geo consistency; hides home IP. | Cost; still detectable; must match locale/timezone. |
| Rotation pool | Higher throughput. | **Out of policy**; evasion; account/legal risk. |

**Recommendation:** normal network. If you travel or need a different geo, use a **single static** egress and keep locale/timezone consistent. No rotation.

**Decision:** ____________________

---

## D9 — Browser default

**Question:** which engine/channel should be the default?

| Option | Pros | Cons |
|---|---|---|
| **Bundled Chromium (recommended)** | No external install; pinned; works on all OSes. | Slightly different from a branded build. |
| Firefox | Diversity. | Patched build; some sites behave differently. |
| WebKit | Safari-like. | Not real Safari; anti-bot behavior may differ. |

**Recommendation:** bundled Chromium only; expose `--engine` for Firefox/WebKit diagnostics. No system Chrome or Edge channel is supported. Do not promise real Safari.

**Decision:** ____________________

---

## D10 — Recruiter emails / profiles

**Question:** include the old project's recruiter email collection?

| Option | Pros | Cons |
|---|---|---|
| **Defer (recommended)** | Keeps v1 focused; avoids personal-data/privacy risk. | Less functionality than the old project. |
| Include | Matches old workflow. | DPDP/GDPR, LinkedIn ToS, data quality, scope creep. |

**Recommendation:** defer. If added later, make it a separate bounded context with a privacy assessment, consent basis, retention, and no LinkedIn auth-wall bypass.

**Decision:** ____________________

---

## D11 — License

**Question:** private or open source?

| Option | Pros | Cons |
|---|---|---|
| Private | No license/compliance work. | No sharing/contributions. |
| MIT | Simple, permissive, common. | Others can reuse without sharing improvements. |
| Apache-2.0 | Patent grant; enterprise-friendly. | Slightly more notice work. |

**Recommendation:** decide before publishing. If this stays personal, private is fine. If public, MIT or Apache-2.0. Do not add a license file until you decide.

**Decision:** ____________________

---

## D12 — Data retention

**Question:** how long do we keep artifacts/logs/postings?

| Data | Recommendation |
|---|---|
| Run artifacts (screenshots/HTML/traces) | 30 days |
| Logs | 90 days |
| Postings | 180 days since last seen (configurable) |
| Exports | User-managed |
| Browser profiles | Until reset/disable |

**Recommendation:** defaults above; add a `joblens cleanup` command in Phase 5. Document retention in the runbook.

**Decision:** ____________________

---

## Default if no response

If you do not answer before Phase 0, the implementation will assume:

- Name: **JobLens**
- LinkedIn: **opt-in, default disabled**
- Indeed: **opt-in, default disabled**
- Fields: **full posting set** listed in D4
- Output: **CSV + Excel**
- Scheduling: **OS scheduler**
- API/dashboard: **defer to v2**
- Egress: **normal network**
- Browser: **bundled Chromium**
- Recruiter emails: **out of scope**
- License: **no license file until decided**
- Retention: **30/90/180 days**

You can change any of these later; the architecture is designed to keep the blast radius small.
