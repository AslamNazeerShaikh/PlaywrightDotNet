# ADR-0007: Compliance-First, Low-Signal Automation

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** Project owner

## Context

The user wants to avoid bans/blocks while scraping. The old Python project measured that headed, persistent Chromium is low-signal, while headless is flagged and CDP/TLS/network signals remain detectable. The target sites have different terms: Naukri is relatively public, Indeed is Cloudflare-protected, and LinkedIn explicitly prohibits automated access without permission. There is no legitimate way to guarantee invisibility.

## Decision

Adopt a **compliance-first, low-signal** policy:

- Headed first contact; persistent profile per source.
- Genuine UA/locale/timezone; no fingerprint spoofing.
- Human pacing, strict caps, one run at a time.
- Detect walls and **stop**; manual solve only.
- No CAPTCHA solving, proxy rotation, account rotation, auth-wall bypass, or retry storms.
- Per-source compliance tiers and consent gates (LinkedIn high-risk, default disabled).

## Consequences

### Positive

- Lower ban/account risk than aggressive automation.
- Predictable, auditable behavior.
- Easier to defend legally/ethically.
- Failures are visible and actionable.

### Negative / trade-offs

- Lower throughput.
- Some sources may be unusable from some networks.
- Manual intervention is required for challenges.
- Not “undetectable”; commercial bot systems may still score the browser.

### Follow-ups

- Enforce caps/quota/circuit breaker in code, not docs only.
- Capture artifacts on every wall.
- Re-check terms/robots quarterly.
- Keep LinkedIn disabled unless explicitly approved.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| “Undetected”/stealth plugins | Inconsistent, brittle, and deceptive; can increase detection. |
| Headless at scale | Explicitly flagged by BotD; Naukri blocks it. |
| Proxy/account rotation | Evasion; legal/account risk; out of policy. |
| CAPTCHA solving | Bypasses an access control; out of policy. |
| Retry through 403/429 | Turns a soft block into a ban. |

## References

- [Anti-Bot & Compliance](../07-anti-bot-and-compliance.md)
- Legacy evidence: `/Users/aslamshaikh/Projects/PythonWebScraping/docs/BOT_DETECTION.md`
