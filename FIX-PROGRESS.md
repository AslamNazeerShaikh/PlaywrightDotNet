# Fix program state (resume file)

Branch: `fix/pr6-pretest-findings` (cut from PR6 head 5ce242c). Checkpoint commit `5b690b7` (phase-e live PASS, awaiting operator merge approval).
Report: `PR6-PRETEST-REPORT.md` (68 findings plus 6-item fix list).
Servers: backend `https://localhost:5208` on fresh DB `/tmp/jobscrape-test/jobscrape.db`, seed admin@example.com. Cockpit `https://127.0.0.1:3000` HTTPS only.

## Model routing (pstack-models.md, exact values)
- Inkling codegen: `opencode/inkling-2-small-free#max` (swapped from Beam per operator, smoke-tested PASS)
- DeepSeek review/gameplan: `opencode-go/deepseek-v4.1-flash#max`
- Muse orchestration: this thread

## Cost ledger (provider console, 3h window ending 2026-11-07)
- DeepSeek v4.1-flash: 483 req, 1.6M in, 30.4M cache read, 601.7k out, $0.70
- Muse Spark: 243 req, 812.1k in, 36.1M cache read, 112.5k out, $0.18
- Beam free: 1213 req, 3.7M in, 76.9M cache read, 323.9k out, $0.00
- Total: about $0.88. Bulk codegen free. Reviews carry the cost.

Go plan snapshot 2026-11-07: monthly 21% (pace 51%), weekly 25%, rolling 4%. Muse 59%, DeepSeek 25%, Beam 15%. 15,080 requests, 2.913B tokens. Extra usage off, $0 credit, so limits pause rather than bill.

## Standing rules
- API limit exhausted error: halt everything, wait for user command. No retries or swaps.
- Fix-review breaker: 6 consecutive FAILs on one item freezes the wave, Muse diagnoses directly.
- Context window: at 60% usage stop everything, no new spawns, report position and wait.
- Commits: conventional format, hook validator on. Deslop plus no-comments deferred to final pre-PR pass.
- API limit exhausted error: halt everything, wait for user command. No retries or swaps.
- Fix-review breaker: 6 consecutive FAILs on one item freezes the wave, Muse diagnoses directly.
- Commits: conventional format, hook validator on. Deslop plus no-comments deferred to final pre-PR pass.

## Wave tracker
- CLOSED: Wave 0 gameplan, operator calls (unify 401, BFF cookie, Viewer default, seed plus dropdown), Wave 4 bearer plane, Wave 1 identity, W1r3, backend cleanup, equal-cost, Wave 3 seed/roles (`1aa2c80`), BFF cookie wave C2 plus R1 (`dbb05e9`, DeepSeek PASS 1-4 plus R1, 154 frontend pass), Wave 5 polish (`c1589c7`, 7 items PASS, 10 red-green plus 4 guards, 168 frontend pass), Wave 6 pins (`de2a8c7`, docs 24 ports, backend 1261 plus frontend 180 pass), Phase E live verify (`5b690b7`, auth matrix plus 6 UI flows PASS, bun start crash noted, lastEmail stale noted).
- RUNNING: W2a fix round (escalation, limiter, tests), W2b fix round (reservation protocol, refund), W2b round-2 verify.
- QUEUED: Wave 5 polish, Wave 6 test pins, Phase E live verify.
- ACCEPTED without rework: completed-409 vs unknown-401 split (operator call), setup-success 2-verify cost, compare early exits.
- VETO WINDOW: granular users/roles policies shipped as Admin-or-grant; user may still veto.
