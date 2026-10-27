---
title: "Turbo Boost: the AI kit for plugin authors"
date: 2026-10-27
pr: turbo-cloud#472
breaks: no
who: "plugin authors who work with an AI assistant; plugins others build on, which can ship `ai/guidelines.md` and `ai/skills/`."
---

**What**: the server ships `ai/` (a plugin guideline and ten Agent Skills, one per kind of plugin
code) and its guides (`docs/**/*.md`) in `plugin-sdk/` beside its assemblies. The template's
`npm run ai` / `ai:update` write the guideline into a `<turbo-boost>` block of `CLAUDE.md` and
`AGENTS.md` (the rest is kept), install the skills in `.claude/skills` and `.agents/skills`
(with those of the plugins in `Dependencies` that ship `ai/`), and add the dev hotel's MCP server
to `.mcp.json`; `npm run ai:rule` records a project rule. `IServerLog`
(`Turbo.Primitives.Logging`) holds the server's latest log entries for a plugin to read.

**Action**: a plugin made from an earlier template copies `tools/ai.mjs` and the three `ai`
scripts of `package.json` from the template, then runs `npm run ai`; `docs/turbo-boost.md`.
