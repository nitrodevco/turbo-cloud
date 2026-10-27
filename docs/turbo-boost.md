# Turbo Boost: the AI kit for plugin authors

- [Introduction](#introduction)
- [1. What is in it](#1-what-is-in-it)
- [2. Installing it in a plugin](#2-installing-it-in-a-plugin)
    - [The agents](#the-agents)
    - [The dev token](#the-dev-token)
- [3. A plugin that ships its own](#3-a-plugin-that-ships-its-own)
- [4. Project rules](#4-project-rules)
    - [Inferring them](#inferring-them)
- [5. The dev tools](#5-the-dev-tools)
- [6. Trying a tool by hand](#6-trying-a-tool-by-hand)

## Introduction

Turbo Boost gives the AI assistant a plugin author works with (Claude Code, Codex, Cursor, GitHub
Copilot, Junie) what it needs to write a Turbo plugin correctly the
first time: the rules and the surface, up front; detailed patterns for each kind of code, when the
task needs them; and tools on the developer's own dev hotel to look things up and see what went
wrong. It is shipped by the server, so what an assistant is told matches the server the plugin
builds against.

## 1. What is in it

| Part | Where | Loaded |
| --- | --- | --- |
| **Guideline**: what a plugin is, the rules the hotel enforces (sources only, `TPP001`-`TPP006`, the contracts), the surface by need, where things go, how to test and check, and to look things up with `search_docs` first | `ai/guidelines/turbo-plugin.md` | upfront, in each agent's instructions file |
| **Skills**: one per kind of code, each a folder with a `SKILL.md` (Agent Skills format: `name` and `description` front matter) | `ai/skills/<name>/SKILL.md` | on demand: the assistant reads one when its description matches the task |
| **Dev tools** over MCP: `search_docs`, `read_doc`, `plugin_errors`, `client_errors`, `server_log`, `message_schema`, `plugin_tables`, `plugin_query` | the MCP server plugin (turbo-mcp-plugin) | called by the assistant |

The skills: `turbo-plugin-basics` (the plugin class, `IHotel`, commands, nodes, settings, tasks,
listeners, tables), `turbo-furni-logic` (`FurniLogic`, `IFurniContext`, `IRoomTurn`),
`turbo-wired-boxes`, `turbo-policies` (policies and post-commit events), `turbo-plugin-messages`,
`turbo-client-modules` (windows, template patches, secondary rooms and carried packets),
`turbo-room-holds-watches`, `turbo-web-routes-admin`, `turbo-mcp-tools`, `turbo-ai-agents` (agents
asking the hotel's language models), `turbo-plugin-tests`, and `turbo-infer-conventions` (reads the
plugin's own conventions and records the approved ones as project rules).

The server ships `ai/` and its guides (`docs/**/*.md`) in `plugin-sdk/` beside its assemblies
(`Turbo.Plugins/Sdk/PluginSdk.targets`), as it ships `Turbo.Plugin.props`: a plugin installs the
kit of the server in its `TurboBin`.

**Kept true.** `AiKitTests` compiles every `csharp` block of each skill and loads it as a plugin,
as the hotel would (the skill's `<!-- compiled as the plugin "<key>" -->` names the key), and
compiles its `csharp test` blocks against it with the test kit; it checks each skill's front matter
and that every guide the kit names is in `docs/`. `scripts/ai-kit-typecheck.mjs` type-checks every
`ts` and `tsx` block against nitro-next's client module SDK with the template's compiler options
(the Docs workflow runs it when `ai/` changes): a block that imports or exports is a module, any
other a fragment of `activate(api)` with `api` declared, and what an HTML comment before the block
declares (`<!-- typed with: declare const roomB: number; -->`). A block fenced `cs` is not compiled.

## 2. Installing it in a plugin

A plugin made from `templates/turbo-plugin`:

```sh
npm run ai                           # install: asks which agents, then writes their files
npm run ai -- --agents cursor,codex  # the same without asking; --agents all for every one
npm run ai:update                    # the same again, from the current TurboBin (after updating the server)
```

What it writes, for each agent chosen:

- **Its instructions file** (the table below): the guideline, the plugin's own guidelines
  (`.ai/guidelines/*.md`) and those of the plugins it depends on, the index of the project rules
  and of the skills, inside one marked block:

  ```
  <turbo-boost>
  ...
  </turbo-boost>
  ```

  Everything outside the block is the author's and is kept as it is; the block is replaced on each
  run. A file without one gets it at the end (a new Cursor rule starts with its front matter,
  `alwaysApply: true`).
- **Skills**: each skill folder, copied into the agent's skills folder. The kit's skills, then the
  dependencies', then the plugin's own `.ai/skills/<name>/` (one with a kit skill's name replaces it).
  A skill the previous run installed and this one does not is removed; a skill folder the kit never
  installed is left alone (`.ai/turbo-boost.json` records what it installed).
- **Its MCP config**: an entry `turbo` for the dev hotel's MCP server at `<hotel>/api/plugins/mcp/mcp`,
  its token from the environment variable `TURBO_MCP_TOKEN`. The file's other servers are kept (in
  Codex's TOML, everything outside the `# <turbo-boost>` block). The address is `--mcp-url <url>`,
  else the last one used, else `http://127.0.0.1:8092/api/plugins/mcp/mcp` (the template README's
  dev hotel).

The choice and the address are kept in `.ai/turbo-boost.json` for `ai:update`. The generated files
may be committed or ignored: both commands write them again.

### The agents

| `--agents` | Agent | Instructions | Skills | MCP config (entry `turbo`) |
| --- | --- | --- | --- | --- |
| `claude` | Claude Code | `CLAUDE.md` | `.claude/skills/` | `.mcp.json`: HTTP, `Authorization: Bearer ${TURBO_MCP_TOKEN}` |
| `codex` | Codex | `AGENTS.md` | `.agents/skills/` | `.codex/config.toml`: `[mcp_servers.turbo]` with `url` and `bearer_token_env_var = "TURBO_MCP_TOKEN"` |
| `cursor` | Cursor | `.cursor/rules/turbo-boost.mdc` | `.cursor/skills/` | `.cursor/mcp.json`: `Authorization: Bearer ${env:TURBO_MCP_TOKEN}` |
| `copilot` | GitHub Copilot (VS Code) | `.github/copilot-instructions.md` | `.github/skills/` | `.vscode/mcp.json`: HTTP, the token asked once (`${input:turbo-mcp-token}`) |
| `junie` | Junie | `.junie/guidelines.md` | `.junie/skills/` | `.junie/mcp/mcp.json`: `npx mcp-remote <url> --header Authorization:Bearer ${TURBO_MCP_TOKEN}` |

Without `--agents`, the first `npm run ai` in a terminal lists them and asks, proposing those whose
folders the plugin has (`.claude`, `.codex`, `.cursor`, `.vscode`, `.junie` or `.idea`, or their
instructions file); later runs and `ai:update` take the saved choice. Not in a terminal (a script,
CI) or with `--no-interaction`, it takes the saved choice, else the agents found, else `claude` and
`codex`.

### The dev token

The MCP server plugin (turbo-mcp-plugin) must be loaded on the dev hotel, with the public web host on
(`Turbo:Web:Enabled`). In the admin panel: **MCP** > **New token** (read-only is enough for the dev
tools), and give your own rank the dev tool nodes (`plugin.mcp.tool.search_docs`,
`plugin.mcp.tool.read_doc`, `plugin.mcp.tool.plugin_errors`, `plugin.mcp.tool.client_errors`,
`plugin.mcp.tool.server_log`, `plugin.mcp.tool.message_schema`, `plugin.mcp.tool.plugin_tables`,
`plugin.mcp.tool.plugin_query`) with `plugin.mcp.use`. Then set
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

writes `.ai/rules/prices-come-from-demosettings.md` and updates the block (`--seen "<files>"` adds
where the code already does it). Edit or delete the files by hand; `npm run ai:update` rebuilds the
index.

### Inferring them

Ask the assistant to learn the plugin's conventions: the skill `turbo-infer-conventions` sweeps the
plugin's code area by area (folder layout, naming, message and node names, settings and texts,
tables, test style, the client module), keeps a pattern only when at least three places follow it
and almost none do otherwise, and shows each as a rule with its count and `file:line` evidence.
Nothing is written until the author approves; each approved rule is recorded with `npm run ai:rule`.

## 5. The dev tools

Read-only MCP tools of the MCP server plugin (`turbo-mcp-plugin`, its README), each with its own
node, for a developer's own hotel:

| Tool | Answers |
| --- | --- |
| `search_docs` | the server's guides (`plugin-sdk/docs`) and the kit's skills, ranked by the words asked: path, heading, the lines around the best match |
| `read_doc` | one guide, or one section of it by heading |
| `plugin_errors` | one plugin's state and each compile error, refused call (`TPP`) and client module build error as `file`, `line`, `column`, `code`, `message`, and its recent warnings and errors from the log |
| `client_errors` | what players' clients reported, newest first: the server's reports of each client module (refused, threw, a template patch that matched nothing or changed nothing) and the console errors the client errors plugin recorded (message, stack, build, browser, page); one plugin's module (by the stack running through `/plugins/<key>/`) and one player's client |
| `server_log` | the latest log lines (level, text filter) and the last error with its stack trace (`IServerLog`) |
| `message_schema` | a plugin message's header and fields, or a core packet's (incoming or outgoing, by name or header) |
| `plugin_tables` | a plugin's tables (by its `TablePrefix`) with columns, types, keys and row counts |
| `plugin_query` | one `SELECT` over one plugin's own tables, checked before it runs (one statement; that plugin's tables only, no schema prefix; no comments, variables, `INTO`, locks or server functions) and run in a read-only transaction, at most 200 rows and 5 seconds; never the MCP plugin's own tables (its tokens) |

The server keeps its latest log entries in memory for them: `IServerLog` (`Turbo.Primitives.Logging`,
2000 entries by the `Logging` filters, given to plugins like any host service).

The guideline tells the assistant to call `search_docs` first and to search the guides folder
(`plugin-sdk/docs`) only when the `turbo` server is not connected.

## 6. Trying a tool by hand

```sh
npm run mcp:inspect                                  # the MCP Inspector's page, connected
npm run mcp:inspect -- --cli --method tools/list     # the tools, in the terminal
```

`npm run mcp:inspect` starts the official MCP Inspector (`@modelcontextprotocol/inspector`) on the
dev hotel's MCP server named in `.mcp.json`, with the token of `TURBO_MCP_TOKEN`: the tools as an
assistant sees them, their schemas, a call and its answer.

`npm run make mcp-tool <Name>` writes a plugin's own tool (`server/Mcp/<Name>Tool.cs`: input and
output records, a read-only `McpTool`, its node) and its test with `McpToolTester`
(`plugin-mcp-tools.md`).
