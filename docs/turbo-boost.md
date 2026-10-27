# Turbo Boost: the AI kit for plugin authors

- [Introduction](#introduction)
- [1. What is in it](#1-what-is-in-it)
- [2. Installing it in a plugin](#2-installing-it-in-a-plugin)
    - [The dev token](#the-dev-token)
- [3. A plugin that ships its own](#3-a-plugin-that-ships-its-own)
- [4. Project rules](#4-project-rules)
- [5. The dev tools](#5-the-dev-tools)

## Introduction

Turbo Boost gives the AI assistant a plugin author works with (Claude Code, Codex, Cursor, any
agent reading `AGENTS.md` and Agent Skills) what it needs to write a Turbo plugin correctly the
first time: the rules and the surface, up front; detailed patterns for each kind of code, when the
task needs them; and tools on the developer's own dev hotel to look things up and see what went
wrong. It is shipped by the server, so what an assistant is told matches the server the plugin
builds against.

## 1. What is in it

| Part | Where | Loaded |
| --- | --- | --- |
| **Guideline**: what a plugin is, the rules the hotel enforces (sources only, `TPP001`-`TPP006`, the contracts), the surface by need, where things go, how to test and check | `ai/guidelines/turbo-plugin.md` | upfront, in the plugin's `CLAUDE.md` / `AGENTS.md` |
| **Skills**: one per kind of code, each a folder with a `SKILL.md` (Agent Skills format: `name` and `description` front matter) | `ai/skills/<name>/SKILL.md` | on demand: the assistant reads one when its description matches the task |
| **Dev tools** over MCP: `search_docs`, `read_doc`, `plugin_errors`, `server_log`, `message_schema`, `plugin_tables` | the MCP server plugin (turbo-mcp-plugin) | called by the assistant |

The skills: `turbo-plugin-basics` (the plugin class, `IHotel`, commands, nodes, settings, tasks,
listeners, tables), `turbo-furni-logic` (`FurniLogic`, `IFurniContext`, `IRoomTurn`),
`turbo-wired-boxes`, `turbo-policies` (policies and post-commit events), `turbo-plugin-messages`,
`turbo-client-modules` (windows, template patches, secondary rooms and carried packets),
`turbo-room-holds-watches`, `turbo-web-routes-admin`, `turbo-mcp-tools`, `turbo-plugin-tests`.

The server ships `ai/` and its guides (`docs/**/*.md`) in `plugin-sdk/` beside its assemblies
(`Turbo.Plugins/Sdk/PluginSdk.targets`), as it ships `Turbo.Plugin.props`: a plugin installs the
kit of the server in its `TurboBin`.

**Kept true.** `AiKitTests` compiles every `csharp` block of each skill and loads it as a plugin,
as the hotel would (the skill's `<!-- compiled as the plugin "<key>" -->` names the key), and
compiles its `csharp test` blocks against it with the test kit; it checks each skill's front matter
and that every guide the kit names is in `docs/`. A block fenced `cs`, `ts` or `tsx` is not compiled.

## 2. Installing it in a plugin

A plugin made from `templates/turbo-plugin`:

```sh
npm run ai          # install: writes the block, the skills and the MCP entries
npm run ai:update   # the same again, from the current TurboBin (run it after updating the server)
```

What it writes:

- **`CLAUDE.md` and `AGENTS.md`**: the guideline, the plugin's own guidelines (`.ai/guidelines/*.md`)
  and those of the plugins it depends on, and the index of the project rules, inside one marked block:

  ```
  <turbo-boost>
  ...
  </turbo-boost>
  ```

  Everything outside the block is the author's and is kept as it is; the block is replaced on each
  run. A file without one gets it at the end.
- **Skills**: each skill folder, copied into `.claude/skills/` (Claude Code) and `.agents/skills/`
  (Codex and the other agents reading the shared Agent Skills folder). The kit's skills, then the
  dependencies', then the plugin's own `.ai/skills/<name>/` (one with a kit skill's name replaces it).
  A skill the previous run installed and this one does not is removed; a skill folder the kit never
  installed is left alone (`.ai/turbo-boost.json` records what it installed).
- **`.mcp.json`**: an entry `turbo` for the dev hotel's MCP server,
  `{ "type": "http", "url": "<hotel>/api/plugins/mcp/mcp", "headers": { "Authorization": "Bearer ${TURBO_MCP_TOKEN}" } }`.
  Other entries in the file are kept. The address is `--mcp-url <url>`, else the last one used,
  else `http://127.0.0.1:8092/api/plugins/mcp/mcp` (the template README's dev hotel).

`--agents claude,codex` limits what it writes (`claude`: `CLAUDE.md`, `.claude/skills`, `.mcp.json`;
`codex`: `AGENTS.md`, `.agents/skills`). The choice and the address are kept in
`.ai/turbo-boost.json` for `ai:update`. The generated files may be committed or ignored: both
commands write them again.

### The dev token

The MCP server plugin (turbo-mcp-plugin) must be loaded on the dev hotel, with the public web host on
(`Turbo:Web:Enabled`). In the admin panel: **MCP** > **New token** (read-only is enough for the dev
tools), and give your own rank the dev tool nodes (`plugin.mcp.tool.search_docs`,
`plugin.mcp.tool.read_doc`, `plugin.mcp.tool.plugin_errors`, `plugin.mcp.tool.server_log`,
`plugin.mcp.tool.message_schema`, `plugin.mcp.tool.plugin_tables`) with `plugin.mcp.use`. Then set
the token where the assistant runs: `TURBO_MCP_TOKEN=tmcp_...`. Never on a live hotel: the log
and table tools are for a developer's own server.

## 3. A plugin that ships its own

A plugin other plugins build on (a games framework, an economy) ships what an assistant needs to use
it, in its folder:

```
ai/guidelines.md              a short guideline: what it offers, its conventions, one snippet each
ai/skills/<name>/SKILL.md     skills, named after the plugin (`<key>-...`)
```

`npm run ai` in a plugin whose `manifest.json` lists it in `Dependencies` (and every plugin those
depend on) finds it beside the plugin (`../<key>`, or a folder whose manifest has that key), or in
the folders of `--plugins <dir>[,<dir>]` / the `TurboPlugins` environment variable, and puts its
guideline in the block under its name and installs its skills. A dependency not found is named in
the output and skipped.

## 4. Project rules

A plugin's own rules, the ones an assistant keeps getting wrong in this code base, are files in
`.ai/rules/` with an index in the block (each rule's first line), so the assistant reads the ones
it needs:

```sh
npm run ai:rule -- "Prices come from DemoSettings" "Never hard-code a price: read DemoSettings.Prices; staff change them on the panel."
```

writes `.ai/rules/prices-come-from-demosettings.md` and updates the block. Edit or delete the files
by hand; `npm run ai:update` rebuilds the index.

## 5. The dev tools

Read-only MCP tools of the MCP server plugin (`turbo-mcp-plugin`, its README), each with its own
node, for a developer's own hotel:

| Tool | Answers |
| --- | --- |
| `search_docs` | the server's guides (`plugin-sdk/docs`) and the kit's skills, ranked by the words asked: path, heading, the lines around the best match |
| `read_doc` | one guide, or one section of it by heading |
| `plugin_errors` | one plugin's state and each compile error, refused call (`TPP`) and client module build error as `file`, `line`, `column`, `code`, `message`, and its recent warnings and errors from the log |
| `server_log` | the latest log lines (level, text filter) and the last error with its stack trace (`IServerLog`) |
| `message_schema` | a plugin message's header and fields, or a core packet's (incoming or outgoing, by name or header) |
| `plugin_tables` | a plugin's tables (by its `TablePrefix`) with columns, types, keys and row counts |

The server keeps its latest log entries in memory for them: `IServerLog` (`Turbo.Primitives.Logging`,
2000 entries by the `Logging` filters, given to plugins like any host service).
