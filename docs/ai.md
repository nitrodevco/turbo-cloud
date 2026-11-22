# Agentic Development

- [Introduction](#introduction)
- [Context Files](#context-files)
- [Giving an Agent a Task](#giving-an-agent-a-task)
- [Documentation for Tools](#documentation-for-tools)

## Introduction

AI coding agents (Claude Code, Codex, Copilot, Cursor) work well on Turbo when they are given the
rules a human contributor follows. The repository carries those rules as files an agent reads,
and this documentation is published in forms an agent can read too.

For plugin work, start with [Turbo Boost](turbo-boost.md), the AI kit the server ships: in a plugin
made from the template, `npm run ai` writes the plugin guideline into its `CLAUDE.md` and
`AGENTS.md`, installs one Agent Skill per kind of plugin code (furni logic, wired boxes, policies,
messages, client modules, tests...), and connects the assistant to your dev hotel's tools
(`search_docs`, `plugin_errors`, `server_log`). Run `npm run ai:update` after you update the server.
A plugin that asks language models while the hotel runs (an NPC, a staff assistant) uses the
[AI SDK](ai-sdk.md). The rest of this page is for work on Turbo itself.

## Context Files

| File | What it holds |
| --- | --- |
| [`AGENTS.md`](../AGENTS.md) | The coding contract: the rules every change needs, review expectations, how to validate a change, and which rules page covers each area. Codex reads it itself |
| [`CONTEXT.md`](../CONTEXT.md) | Where code belongs and which project may depend on which |
| `docs/rules/` | The rules for one area: [grains](rules/grains.md), [room objects](rules/room-objects.md), [wired](rules/wired.md), [inventory](rules/inventory.md), [types and constants](rules/types-and-constants.md), [packet handlers](rules/handlers.md), [host features](rules/host-features.md), [test helpers](rules/testing.md), and the [drift checklist](rules/drift.md). An agent reads only the pages for what it touches |
| `.claude/rules/` | One short reminder per area, each loaded by Claude Code only when it opens a file of that area (its `paths:` frontmatter), pointing at the area's page. They hold no rules of their own |
| [`docs/patterns/`](patterns/README.md) | One real-shaped sample each of a packet handler, a service and a unit test, and the task recipes |
| `CLAUDE.md`, `.github/copilot-instructions.md` | Short adapters that point Claude Code and Copilot at the files above; `CLAUDE.md` imports `AGENTS.md` and `CONTEXT.md` |

A plugin made from the template has its own `README.md` listing its commands and recipes; give an
agent that, and this site's pages for the parts it touches.

## Giving an Agent a Task

The same recipe works with any tool:

1. Say what you want done and name the files it should change.
2. Attach `AGENTS.md` and `CONTEXT.md`.
3. Point it at the closest sample in `docs/patterns/`, or the closest real file.
4. Ask for the failure cases to be handled and for the commands that prove it works (the test it
   added, `dotnet test --filter ...`).

Review what comes back as you would any pull request: the [Contribution Guide](contributions.md)
asks you to say an AI helped, and to be able to explain the code in your own words.

## Documentation for Tools

Every page of the published site has a **Copy as markdown** button and a markdown twin beside it
(`/installation.md` for `/installation`). The site also serves:

- `/llms.txt`: the sidebar as a list of links, each with a one-line summary;
- `/llms-full.txt`: every page in sidebar order, in one file.

Give an agent `llms-full.txt` when it needs the whole framework, or the page it needs when it
doesn't.
