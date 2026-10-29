# ADR-0006: Browser-First with Optional API Fast Path

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** Project owner

## Context

Naukri, LinkedIn, and Indeed are JavaScript-heavy and protected by different anti-bot layers (Akamai, internal auth/rate limits, Cloudflare). Raw HTTP clients can be faster, but they fail on JS challenges and expose a TLS/HTTP fingerprint that does not match the claimed browser. The old project found a Naukri private search API (`jobapi/v3/search` with an RSA `nkparam`) useful but fragile. There is no general public jobs API for these sites.

## Decision

Make **browser automation the default and source of truth**. Allow an **optional, feature-flagged Naukri API fast path** only after a time-boxed spike proves it stable/compliant; always fall back to the browser. Do not build the architecture around private APIs.

## Consequences

### Positive

- Works with JS/SPA pages and real sessions.
- One consistent access path across sources.
- Persistent profiles and challenge handling are reusable.
- API outage/rotation does not break the product.

### Negative / trade-offs

- Slower than raw HTTP.
- Browser resource usage.
- More moving parts (profiles, challenges, artifacts).
- Private API path adds maintenance/ToS risk if enabled.

### Follow-ups

- Keep `IJobSource` as the seam for a future hosted/API adapter.
- Feature-flag the Naukri fast path (default off).
- On 403/406, disable the fast path and fall back.
- Never use an API to bypass a wall/login/rate limit.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| HTTP-only (JobSpy-style) | Fragile under JS/WAF; TLS mismatch; no challenge handling. |
| Managed scraping API | Cost/vendor/ToS dependency; keep as a later adapter, not the core. |
| Official API only | Not generally available for these sites/use case. |
| Browser-only, no API seam | Less flexible; a fast path may be useful later. |

## References

- [Research Analysis](../01-research-analysis.md)
- [Site Playbooks](../08-site-playbooks.md)
