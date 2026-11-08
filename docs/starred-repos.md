# Starred repos — AslamNazeerShaikh

Source: <https://github.com/AslamNazeerShaikh?tab=stars> — 7 starred repos, snapshot 2026-11-08. Stats below are live at snapshot time and will drift; links do not.

## 1. obra/superpowers — <https://github.com/obra/superpowers>

An agentic skills framework and software development methodology that works. The complete SDLC loop for coding agents: brainstorming, git worktrees, plan writing, subagent-driven execution, TDD, review, and branch finishing.
Language: Shell · License: MIT · Stars: 314,338 · Forks: 27,969 · Updated: 2026-11-08.
Topics: ai, brainstorming, coding, sdlc, skills, subagent-driven-development, superpowers.
Use in this repo: installed as the git-backed OpenCode plugin in `.opencode/opencode.json`; drives the session workflow (this repo's plans, SDD runs, and reviews execute through it).

## 2. mattpocock/skills — <https://github.com/mattpocock/skills>

Skills for Real Engineers, straight from the author's `.agents` directory. Small, sharp skill files including the grilling interview, domain modeling, and codebase-design vocabulary this repo borrows.
Language: Shell · License: MIT · Stars: 318,105 · Forks: 26,624 · Updated: 2026-11-08. Homepage: <https://aihero.dev/skills>.
Use in this repo: 5 skills vendored under `.opencode/skills/` (`grill-with-docs`, `improve-codebase-architecture`, `grilling`, `domain-modeling`, `codebase-design`).

## 3. affaan-m/ECC — <https://github.com/affaan-m/ECC>

The agent harness performance optimization system. Skills, instincts, memory, security, and research-first development for Claude Code, Codex, OpenCode, Cursor and beyond. Homepage: <https://ecc.tools>.
Language: JavaScript · License: MIT · Stars: 299,397 · Forks: 44,232 · Updated: 2026-11-08.
Topics: ai-agents, anthropic, claude, claude-code, developer-tools, llm, mcp, productivity.
Use in this repo: ECC command pack in `.opencode/commands/`, 8 agents in `.opencode/opencode.json`, rules in `.opencode/instructions/INSTRUCTIONS.md` (see `docs/18-harness-packs.md`).

## 4. backnotprop/pstack — <https://github.com/backnotprop/pstack>

Skills and principles for rigorous AI-assisted engineering with Claude, Codex, and Pi. Role-to-model routing, playbooks, and subagent discipline.
Language: TypeScript · License: MIT · Stars: 3,802 · Forks: 266 · Updated: 2026-11-08.
Topics: agent-skills, agentskills, ai-agents, claude-code, claude-skills, codex, cursor, opencode, pstack, skills.
Use in this repo: project-local core in `.opencode/` (skills, agents, commands, prompts); model routing in `.opencode/pstack-models.md` governs every subagent regardless of pack (see `docs/17-pstack-opencode.md`).

## 5. Graphify-Labs/graphify — <https://github.com/Graphify-Labs/graphify>

Turn any codebase, with its docs, SQL schemas, configs, and PDFs, into a queryable knowledge graph. A `/graphify` skill for Claude Code, Cursor, Codex, and Gemini CLI: local deterministic AST parsing, every edge explained, no vector store. Homepage: <https://www.graphify.com>.
Language: Python · License: Apache-2.0 · Stars: 144,190 · Forks: 13,802 · Updated: 2026-11-08.
Topics: ai-agents, ast, code-analysis, code-search, graphrag, knowledge-graph, leiden, llm, mcp, skills, tree-sitter.
Use in this repo: knowledge graph in `graphify-out/` (`graphify query/path/explain`), refreshed via hooks; generated output stays dirty and uncommitted.

## 6. DietrichGebert/ponytail — <https://github.com/DietrichGebert/ponytail>

Makes your AI agent think like the laziest senior dev in the room. The best code is the code you never wrote. YAGNI-flavored agent skills and prompt engineering. Homepage: <https://ponytail.dev>.
Language: JavaScript · License: MIT · Stars: 186,596 · Forks: 10,335 · Updated: 2026-11-08.
Topics: agent-skills, ai-agents, claude, claude-code, claude-code-plugin, cursor-rules, developer-tools, llm, prompt-engineering, yagni.
Use in this repo: none currently — candidate if we ever want a "write less code" skill pack.

## 7. cathrynlavery/diagram-design — <https://github.com/cathrynlavery/diagram-design>

Editorial diagram design for Claude Code, Codex, GitHub Copilot, Cursor, Factory Droid, Qoder, and Pi. 45 diagram types. Self-contained HTML + SVG. No shadows. No Mermaid slop. Homepage: <https://diagramdesign.dev>.
Language: HTML · License: MIT · Stars: 65,750 · Forks: 4,357 · Updated: 2026-11-08.
Topics: agent-skills, claude-code, codex, data-visualization, diagrams, drawio, mermaid, svg.
Use in this repo: none currently — the architecture review HTML reports already follow its editorial style (Tailwind + hand-built visuals, Mermaid only for graphs).
