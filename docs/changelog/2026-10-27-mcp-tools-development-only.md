---
title: "MCP tools for development servers only"
date: 2026-10-27
pr: turbo-cloud#478
breaks: no
who: "plugins with MCP tools for their authors (a server's log, its tables)."
---

**What**: an MCP tool that overrides `DevelopmentOnly => true` is registered only when the
server's host environment is `Development`; on any other server it is not listed and not
callable, whatever nodes a staff member holds. `IMcpTool` has the new member.

**Breaks plugins**: A plugin implementing `IMcpTool` itself rather than deriving from
`McpTool<TInput>` adds the member.

**Action**: none; mark such tools `DevelopmentOnly` (`docs/plugin-mcp-tools.md`).
