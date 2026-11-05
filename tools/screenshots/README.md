# Screenshot capture

Recaptures every ops-deck route, light plus dark, as full-page PNGs.

## Use

1. Start the API and the BFF (`dotnet run --project src/backend/JobScrape.Api`, `bun run dev` in `src/frontend`).
2. Start headless Chrome with remote debugging:
   macOS: `/Applications/Google\ Chrome.app/Contents/MacOS/Google\ Chrome --headless --remote-debugging-port=9222 --ignore-certificate-errors --user-data-dir=/tmp/chrome-shots about:blank`
   Linux: `google-chrome` with the same flags.
3. Run `bun tools/screenshots/capture.ts` from the repo root.
4. Check one light and one dark file by eye, then commit the PNGs.

Env knobs: `BFF_URL` (default `https://127.0.0.1:3000`), `SHOT_OUT`
(default `docs/screenshots`), `CDP_PORT` (default `9222`).

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
