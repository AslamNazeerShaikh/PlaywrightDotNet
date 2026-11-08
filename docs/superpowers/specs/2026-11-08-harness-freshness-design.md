# Harness freshness check-then-sync — design

**Intent:** Check the four harness upstreams for real movement, then sync only what meaningfully changed, and push. **Why:** general freshness only — nothing is broken, so churn must earn its place.

**Goals:** A per-pack delta (upstream version/commit vs ours, restricted to our vendored subset); selective sync via each pack's own method; the `docs/18-harness-packs.md` validation suite green; one or two commits, pushed.
**Non-goals:** Product-code changes; harness re-architecture; new packs or skills.
**Anti-goals:** Verbatim overwrites that clobber local OpenCode-only adaptations; syncing files whose only delta is upstream wording for other harnesses.

**Constraints:** pstack/ECC syncs are manual three-way merges, never clean pulls (`SYNC-LOG.md`, `docs/18-harness-packs.md`); mattpocock re-vendor keeps the MIT attribution lines; Superpowers updates via the plugin CLI against project config only; model routing follows `.opencode/pstack-models.md` (recon on inkling, chores on beam-free).

**Approaches:** (A) full sync in one cut — rejected: blind churn against a freshness-only motive, hard to review. (B) staged easy-first — rejected for now: still syncs before knowing what moved. (C) check first, sync selectively — picked: recon costs about as much as B's first stage, and every applied diff is reviewable on its merits. Owner chose C.

**Decided parts:** Check order is superpowers (plugin check command), mattpocock (diff our 10 files), ECC (fresh-clone diff of `commands/`, `prompts/agents/`, `instructions/`), pstack (mirror commits since `f104aa8`/0.15.36). "Meaningful" means changes touching our vendored subset, or fixes/features affecting OpenCode V2 — pure other-harness wording does not qualify.

**Left to the builder:** Exact diff commands per pack, per-file apply/skip calls with reasons, commit shape (`chore(harness): ...` per pack or one combined), and the validation run output.
