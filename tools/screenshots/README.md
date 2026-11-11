# Screenshot capture

Recaptures every ops-deck route, light plus dark, as full-page PNGs.

## Use

1. Start the API and the BFF (`dotnet run --project src/backend/JobScrape.Api`, `bun run dev` in `src/frontend`).
2. Run `bun tools/screenshots/capture.ts` from the repo root. The script
   launches the Playwright bundled Chromium itself with remote debugging and
   shuts it down at the end. No system browser is used. Requires a bundled
   Chromium install (`cli.js install chromium` from the API build output,
   see docs/09-operations-runbook.md §0, or `bunx playwright install chromium`
   in `src/frontend`).
3. Check one light and one dark file by eye, then commit the PNGs.

Env knobs: `BFF_URL` (default `https://127.0.0.1:3000`), `SHOT_OUT`
(default `docs/screenshots`),
`PLAYWRIGHT_BROWSERS_PATH` (default: the OS ms-playwright cache —
`~/Library/Caches/ms-playwright` on macOS, `%LOCALAPPDATA%\ms-playwright`
on Windows, `~/.cache/ms-playwright` on Linux).

The CDP port is ephemeral: Chromium picks it at launch and reports it in
`DevToolsActivePort` inside the throwaway profile, so a leftover system
browser squatting on a fixed port is never driven by mistake.

The script picks the highest installed `chromium-*` revision and a throwaway
profile directory under the OS temp dir, removed on exit.

## How it works

One CDP tab walks the hash routes. Each stop waits for probes to settle,
screenshots light, clicks the THEME toggle, then screenshots dark. Height
comes from the footer bottom at a tall viewport, so each file fits its page
instead of a fixed 1080 crop. The theme step polls the toggle label until it
reads the wanted theme. A fixed sleep raced React state once and produced
dark files with light pixels, so the poll stays.

## Improve later

Route list, selectors, and settle times are constants at the top of
`capture.ts`. A new page means one more entry in `routes`.
