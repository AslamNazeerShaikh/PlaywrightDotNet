# Bundled-only browsers + uBOL max for Naukri — Spec

**Goal:** Use only Playwright bundled browsers. No system Chrome or Edge. Run Naukri on headed bundled Chromium with uBlock Origin Lite at max blocking.

**Status:** Research complete 2026-11-11. Four parallel researchers probed the tree read-only.

## Decisions

1. Frontend e2e drops `channel: "chrome"`. It launches Playwright bundled Chromium v1254 (Chrome for Testing 158.0.8127.18, @playwright/test 1.65.0). Fresh machines run `bunx playwright install chromium`. No `package.json` version change.
2. Backend keeps `BrowserChannel` with `Bundled` only. Delete `Chrome` and `Edge` members. Never reuse ordinals. `Channel()` mapping deleted. Launch always passes `Channel: null`. Stored `Chrome` or `Edge` rows migrate to `Bundled` via data-only EF migration. Read path never throws on old strings because migration rewrites them.
3. Naukri runs headed bundled Chromium. `Headless` stays `false`. uBOL max stays `ubol-max` with `ExtensionDir` pointing at `vendor/ubol-chrome-max`. Committed `appsettings.json` stays `AdblockMode: null`. Local `appsettings.Local.json` opts in. Production opt-in uses same keys via environment. Max means 16 of 56 rulesets: `ublock-filters`, `easylist`, `easyprivacy`, `pgl`, `ublock-badware`, `urlhaus-full`, `adguard-spyware-url`, `ublock-experimental`, `ind-0`, plus 7 `annoyances-*`. Pinned tag `2026.914.1325`.
4. Docs and screenshot tool stop naming system Chrome. Install docs gain darwin-arm64 and win64 paths. Screenshot tool launches bundled Chromium over CDP. No CI is added. Design-target CI text stays marked as target.

## Non-goals

- No Playwright version alignment (backend 1.64.1 vs frontend 1.65.0 stays).
- No `BrowserChannel` enum deletion (keep one-member enum plus column).
- No committed `AdblockMode: ubol-max` default (fresh clones stay unblocked).
- No `vendor/ubol-chrome-max` rename (Chrome-flavored name is cosmetic).
- No CI workflow creation.

## Acceptance

- `bun run test:e2e` passes with no Chrome installed and `chromium-1254` marked `INSTALLATION_COMPLETE`.
- `dotnet run --project src/backend/JobScrape.Domain.Tests` passes with only `Bundled` tests. `dotnet run` for Application, Infrastructure, Api suites passes.
- Headed bundled Chromium launches with `--load-extension` flags when local opt-in present. Missing dir fails startup. Mid-run loss logs once and launches without blocker.
- `grep -rn` for `Chrome|Edge|msedge|"chrome"` in `src/` and `vendor/*.sh` returns only historical `docs/01-research-analysis.md` and applied migrations.
- Existing `Chrome` or `Edge` rows read back as `Bundled` after migration.
