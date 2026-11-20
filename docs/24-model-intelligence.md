# Model intelligence — OpenCode / Go 8-model comparison

> Verified 2026-11-18 against `artificialanalysis.ai` (Intelligence Index v5/v5.0.1) and `huggingface.co/models`, plus live `opencode models` catalog for provider + thinking variants.
> Pricing ignored here — all 8 are free or cheap under the current OpenCode/Go subscription. Question answered: which is smarter / most capable for complex coding, thinking, and tool use.
> Opencode data (`opencode.ai/data`) tracks usage/pricing only, not intelligence — hence this independent check.

## 4-column × 9-row comparison

| # | Model (OpenCode ID) | Intelligence — AA Index v5 + key coding bench | Hugging Face — params / license / I-O | OpenCode availability — provider + thinking efforts |
|---|---|---|---|---|
| 1 | Beam 1.0 (`beam-free`) | AA **18** (#22/32, v5.0.1; was 30 on v4.3). TB 4.0 3.0%, FrontierCode 5.5%, Briefcase 976 Elo — weakest coder here | `reflection/Beam-1.0-501B-A23B` — 501B/23.4B MoE, 1M ctx, **Apache-2.0**, text→text only | `opencode/beam-free` + `opencode-go/beam-free` — `low, medium, high, xhigh, max` |
| 2 | Hy4 flash Free (`hy4-flash-free`) | **No AA score** (released 2026-11-16). Full Hy4 = **35** (#2 China). Vendor: WorkIndex 42.1 vs preview 39.7, Open 48.3 vs DeepSeek 56.5 | `tencent/Hy4-flash` — 220B/11.3B active, 1M, **Tencent Hy License**, image+text→text | `opencode/hy4-flash-free` + `opencode-go/hy4-flash-free` — `none, high` only |
| 3 | DeepSeek V4.1 Flash 1017 (`deepseek-v4.1-flash`) | AA **33 max / 29 high** (#2/32). DeepSWE 77.0, TB 4.0 42.3 (Claude Code), FrontierCode 28.5%, Briefcase 1570 | `deepseek-ai/DeepSeek-V4.1-Flash-1017` — 552B/16B MoE, 1M, **MIT**, image+text→text | `opencode/deepseek-v4.1-flash-free` + `opencode-go/deepseek-v4.1-flash` — `low, high, max` |
| 4 | Inkling 2 Small (`inkling-2-small-free`) | AA **31** (#4/32). DeepSWE 72.1, SWE-Atlas 61.9, Pro-Hard 86.3, MMMU-Pro 81.6, Briefcase 1527 | `thinkingmachines/Inkling-2-Small` — 594B/17B MoE, 1M, **Apache-2.0**, image+text→text | `opencode/inkling-2-small-free` only — `none, minimal, low, medium, high, max` (no `xhigh`) |
| 5 | MiMo-V2.6-Flash (`mimo-v2.6-flash`) | AA **27** (#12/32). DeepSWE 67.9, TB 4.0 28.8, Toolathlon 73.6 | `XiaomiMiMo/MiMo-V2.6-Flash-RL` — 309B/15B MoE, 1M, **MIT**, omni text+image+video+audio→text | `opencode/mimo-v2.6-flash-free` + `opencode-go/mimo-v2.6-flash` — `[]` fixed, no picker |
| 6 | Qwen3.8 Flash (`qwen3.8-flash`) | AA **29** (`qwen3-8-flash-next`, #9/32; ~40 max). DeepSWE 58.7, SWE-Pro 62.5, GPQA 91.7 | `Qwen/Qwen3.8-Flash-Next` — 180B/6B active, 262K→1M YaRN, **qwen-community-1.0**, image+video+text→text | `opencode-go/qwen3.8-flash` only — `none, low, medium, xhigh` |
| 7 | Muse Spark 1.3 Contributor (`muse-spark-1.3-contributor`) | AA **37 max** (#55/92; 61–62 at Sept launch, frontier). Banking #1, GPQA 94%, TB 2.1 86% | No HF (proprietary). Sibling `meta-models/Muse-Spark-Open-816B-A42B` 816B/42B. 1M, text+image+video→text | `opencode-go/muse-spark-1.3-contributor` only — `minimal, low, medium, high, xhigh` |
| 8 | MiniMax M3.1 Flash (`minimax-m3.1-flash`) | AA **29 default, 42–43 max** (#7/32). DeepSWE 73.7, OSWorld 61.7, Automation 62.8 | `MiniMaxAI/MiniMax-M3.1-Flash` — 428B/23B MoE, 1M, **minimax-community**, image+video+text→text | `opencode-go/minimax-m3.1-flash` only — `low, medium, high, xhigh, max` |

## Analysis

1. Smartest overall: **Muse Spark 1.3 (37)** — frontier tier, best for complex reasoning. Closed weights, data-sharing contributor pricing.
2. Best open coder: **DeepSeek 1017 (33)** — DeepSWE 77.0 + MIT + 1M. Default for hard coding tasks.
3. Best agent/vision/finance: **Inkling 2 Small (31)** — BrowseComp 90.2%, MCP Atlas 85%, DesignArena #1, Apache-2.0. Second coder at 72.1.
4. Productivity pick: **Hy4 flash** — no AA yet, vendor slightly above Hy4-preview, well below Hy4/DeepSeek on open benches. Use for slides/docs/everyday, not long-horizon SWE.
5. Middle tier: **MiniMax (29/42 max) > Qwen (29/40 max) > MiMo (27)** on code; Qwen leads exams/multilingual.
6. Last: **Beam (18)** — text-only, TB 4.0 3.0%. Demoted from chores 2026-11-21 after local 6-round bench (longcat-2.5-free 4–2 over beam-free#max); chores now `opencode-go/longcat-2.5-free` in `pstack-models.md`.

## References

- AA models: [inkling-2-small](https://artificialanalysis.ai/models/inkling-2-small) (31), [deepseek-v4-1-flash-1017](https://artificialanalysis.ai/models/deepseek-v4-1-flash-1017) (33), [minimax-m3-1-flash](https://artificialanalysis.ai/models/minimax-m3-1-flash) (29), [mimo-v2-6-flash](https://artificialanalysis.ai/models/mimo-v2-6-flash) (27), [qwen3-8-flash-next](https://artificialanalysis.ai/models/qwen3-8-flash-next) (29), [muse-spark-1-3](https://artificialanalysis.ai/models/muse-spark-1-3) (37), [reflection-beam](https://artificialanalysis.ai/models/reflection-beam) (18), [tencent-hy4](https://artificialanalysis.ai/models/tencent-hy4) (35, full only)
- HF: [tencent/Hy4-flash](https://huggingface.co/tencent/Hy4-flash), [thinkingmachines/Inkling-2-Small](https://huggingface.co/thinkingmachines/Inkling-2-Small), [deepseek-ai/DeepSeek-V4.1-Flash-1017](https://huggingface.co/deepseek-ai/DeepSeek-V4.1-Flash-1017), [XiaomiMiMo/MiMo-V2.6-Flash-RL](https://huggingface.co/XiaomiMiMo/MiMo-V2.6-Flash-RL), [Qwen/Qwen3.8-Flash-Next](https://huggingface.co/Qwen/Qwen3.8-Flash-Next), [MiniMaxAI/MiniMax-M3.1-Flash](https://huggingface.co/MiniMaxAI/MiniMax-M3.1-Flash), [reflection/Beam-1.0-501B-A23B](https://huggingface.co/reflection/Beam-1.0-501B-A23B)
- Vendor research: [hy.tencent.ai/research/hy4-flash](https://hy.tencent.ai/research/hy4-flash), [thinkingmachines.ai/news/inkling-2-small](https://thinkingmachines.ai/news/inkling-2-small/)
- Usage/pricing (not intelligence): [opencode.ai/data](https://opencode.ai/data/#leaderboard), [compare hy4-flash vs inkling-2-small](https://opencode.ai/data/compare/tencent/hy4-flash/thinkingmachines/inkling-2-small)
- Repo role map: `.opencode/pstack-models.md`
