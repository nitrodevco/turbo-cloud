---
title: "Turbo Boost: Cursor, Copilot and Junie; conventions; an MCP tool recipe; the Inspector"
date: 2026-10-27
pr: turbo-cloud#477
breaks: no
who: "plugin authors who work with an AI assistant."
---

**What**: `npm run ai` asks which agents to set up (or `--agents claude,codex,cursor,copilot,junie|all`)
and writes each one's own instructions file, skill folder and MCP config (`.cursor/rules/turbo-boost.mdc`,
`.github/copilot-instructions.md` and `.vscode/mcp.json`, `.junie/guidelines.md`,
`.codex/config.toml`), each keeping what the author wrote. The guideline has the assistant call
`search_docs` first. A new skill, `turbo-infer-conventions`, records a plugin's own conventions as
rules once the author approves them (`npm run ai:rule ... --seen <files>`). `npm run make mcp-tool
<Name>` writes a tool and its `McpToolTester` test; `npm run mcp:inspect` opens the MCP Inspector
on the dev hotel's MCP server. The skills' TypeScript blocks are type-checked.

**Breaks plugins**: Without `--agents` and outside a terminal, `npm run ai` now sets up the
agents whose folders the plugin has (else Claude Code and Codex, as before).

**Action**: a plugin made from an earlier template copies `tools/ai.mjs`, `tools/make.mjs`,
`tools/mcp-inspect.mjs` and the `mcp:inspect` script of `package.json` from the template;
`docs/turbo-boost.md`.
