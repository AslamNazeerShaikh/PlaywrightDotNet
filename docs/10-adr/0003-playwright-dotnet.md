# ADR-0003: Use Playwright for .NET

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** Project owner

## Context

The target sites are JavaScript-heavy and protected by anti-bot layers. The solution needs a real browser, persistent sessions, cross-platform support, reliable waiting/locators, and strong debugging artifacts. The owner knows C#/.NET, not a Node-first automation stack. Playwright .NET `1.64.1` supports Chromium, Firefox, and WebKit on macOS/Linux/Windows, with persistent contexts, tracing, codegen, and auto-waiting.

## Decision

Use **Playwright for .NET `1.64.1`** as the browser automation engine. Chromium is the default production engine; Firefox/WebKit are compatibility/diagnostic targets. Use headed first contact and persistent contexts per source.

## Consequences

### Positive

- One .NET API for three engines.
- Auto-wait and web-first assertions reduce flaky sleeps.
- Persistent contexts keep cookies/session state.
- Tracing/screenshots/network capture make wall diagnosis possible.
- Official xUnit v3 integration.

### Negative / trade-offs

- Playwright's WebKit is **not** real Safari; real Safari automation requires WebDriver on macOS.
- Playwright's Firefox is a patched build, not branded Firefox.
- Browser binaries are version-coupled and large; upgrades require reinstall.
- OS support is stricter than .NET (Windows 11+, macOS 14+).
- CDP/automation signals remain detectable; this is not a stealth tool.

### Follow-ups

- Pin Playwright and reinstall browsers together.
- Run the browser matrix in CI.
- Document the Safari limitation.
- Never add fingerprint-spoofing packages.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| Selenium | Viable, but weaker auto-wait/tracing/persistent-context ergonomics; only needed for real Safari. |
| Puppeteer | Node-first; .NET support is weak/unofficial. |
| Raw HTTP + TLS impersonation | Fragile on JS/WAF; better as an optional Naukri API fast path. |
| Managed scraping API | Cost/ToS dependency; keep as a later adapter, not the foundation. |

## References

- <https://playwright.dev/dotnet/>
- <https://playwright.dev/dotnet/docs/browsers>
- [Research Analysis](../01-research-analysis.md)
