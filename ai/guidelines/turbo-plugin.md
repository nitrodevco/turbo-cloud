## Turbo plugin development

This repository is a **Turbo plugin**: one folder that adds a feature to a Turbo hotel (server in
C#, client in TypeScript/React). The hotel compiles the server half from its sources, builds the
client module from `client/src/`, serves `wwwroot/`, seeds `gamedata/`, and takes all of it away
when the plugin unloads. Write against the plugin surface only; never against the server's internals.

### Layout

| Path | What |
| --- | --- |
| `manifest.json` | `Name`, `Key` (lower case: the prefix of everything the plugin names), `Version`, `Dependencies`, `TablePrefix`, `ApiVersion: 1`, `Sources` (default `server/**/*.cs`) |
| `server/*.cs` | the server half: `npm run make` puts each kind in its folder (`Commands/`, `Messages/`, `Logic/`, `Wired/`, `Policies/`, `Routes/`, `Tasks/`, `Listeners/`, `Database/`) |
| `client/src/index.tsx` | the client module (`defineModule` from `@nitrodevco/client-module-sdk`); imports only its own files, the SDK, `react`, `zustand` |
| `wwwroot/` | files clients load (icons, `template-patches.json`); never JavaScript or an assembly |
| `gamedata/external_texts.txt`, `external_variables.json` | the plugin's texts and client variables, staff can change them |
| `tests/<Name>.Tests/`, `client/tests/` | xunit tests against `FakeHotel`; `node --test` for client logic |

### Rules the hotel enforces

- **Sources only.** No `.dll`, `.exe`, `.so`, `.wasm` or `.nupkg` anywhere in the folder (the load is
  Rejected). No NuGet package of the plugin's own: it compiles against .NET + ASP.NET Core,
  `Turbo.Contracts`, `Turbo.Primitives`, `Turbo.Database`, EF Core (MySQL), Orleans abstractions.
  `using Turbo.Rooms`, `Turbo.Players`, `Turbo.Catalog`... fails with CS0234: use the contracts.
- **Refused calls** (`TPP` errors, file and line): TPP001 loading assemblies, TPP002 native code,
  TPP003 `unsafe`/pointers, TPP004 `Reflection.Emit`, TPP005 `Process.Start`, TPP006 Roslyn.
- C# 14, nullable on, no unsafe. Generators available: `[GenerateSerializer]`, `[LoggerMessage]`,
  `[JsonSerializable]`, `[GeneratedRegex]`.
- **Found, not registered.** Public, concrete, top-level classes are found as the plugin loads:
  commands, permission node sources, packet and message handlers, furni logic, wired boxes, room
  event listeners, games, policies, event handlers, web routes, MCP tools, scheduled tasks,
  `[PluginSettings]` classes, a `PluginDbContextBase<T>`. Each is built by constructor from the
  plugin's own services, then the host's (`IHotel`, `IGrainFactory`, `TimeProvider`, `ILogger<T>`).
  Register only the plugin's own services, in `TurboPlugin.Services`.
- **Names carry the key**: messages `<key>.<name>`, permission nodes `<key>.<...>` (an
  `IPermissionNodeSource` with `Prefix => "<key>"`) or `plugin.<key>.<name>` (`IPluginAdmin`),
  texts `<key>.<...>`, tables `<TablePrefix>...`, web routes `/api/plugins/<key>/...`.
- **Turns.** Room code (commands, furni logic, wired, listeners, arrival and effect policies) runs
  in the room's turn: never await the hotel API on that same room, and never read the database
  there. `IHotel` members say where they may be called (`[HotelCall]`): `Anywhere`,
  `NotFromItsGrain`, `OffGrain` (reads the database: never in a room's turn).
- **Behaviour is Habbo's.** A setting is for real hotel policy (a price, a limit, on/off), never to
  restore old behaviour. A permission node gates who may do something; never check ranks by number.
- Versions stay 1 (`ApiVersion`, client `api.version`); no compatibility shims.

### The surface, by need

| Need | Use | Skill |
| --- | --- | --- |
| everyday hotel actions (players, rooms, wallet, furni, badges, notices, clock) | `IHotel` by constructor; `FakeHotel` in tests | turbo-plugin-basics |
| a chat command, a permission node, a setting, a scheduled task, a room listener, tables | `ICommand<TArgs>`, `IPermissionNodeSource`, `[PluginSettings]`, `IScheduledTask`, `IRoomEventListener`, `PluginDbContextBase<T>` | turbo-plugin-basics |
| furni that does something | `FurniLogic` + `[FurniLogic("key")]`, `IFurniContext`/`IRoomTurn` | turbo-furni-logic |
| wired boxes | `WiredEffect`/`WiredTrigger<T>`/`WiredCondition`/`WiredSelector` + `[WiredBox]` | turbo-wired-boxes |
| allow, refuse or change a hotel action | `IPurchasePolicy`, `IRoomEntryPolicy`, `IRoomArrivalPolicy`, `IPlacementPolicy`, ...; `IEventHandler<T>` after it | turbo-policies |
| client and server halves talking | `[PluginMessage("<key>.x")]` records, `IPluginMessageHandler<T>`, `api.messages` | turbo-plugin-messages |
| UI in the client | `client/src` module: windows, toolbar, links, template patches, secondary rooms | turbo-client-modules |
| keep a room loaded, show a player another room | `IHotel.Rooms.HoldAsync` / `WatchAsync` | turbo-room-holds-watches |
| HTTP for players or a site, staff pages | `IPluginWebRoute`, `IPluginAdmin` | turbo-web-routes-admin |
| tools for AI assistants | `McpTool<TIn, TOut>` | turbo-mcp-tools |
| ask language models: an NPC, a helper, a staff assistant, typed answers | `IAi`, `AiAgent`, `AiAgent<TOut>`, `AiFake` in tests | turbo-ai-agents |
| tests | xunit + `FakeHotel`, `McpToolTester` | turbo-plugin-tests |

Load the skill before writing that kind of code.

### Commands

- `npm run make <kind> <name>`: scaffolds one piece in the right folder and shape (`command`,
  `message`, `logic`, `wired`, `listener`, `setting`, `route`, `window`, `patch`, `migration`,
  `schedule`, `policy`, `mcp-tool`). Prefer it to writing the file from nothing.
- `npm run check`: compiles the server sources exactly as the hotel does (same references, C#,
  TPP rules). Run it after every server change; errors read `server/File.cs(line,col)`.
- `npm test`: the server tests (`dotnet test`) and the client tests. `npm run typecheck`: the module's types.
- `npm run dev`: keeps `dist/<key>/` current for a dev hotel, which reloads the plugin on save.
- `npm run mcp:inspect`: the MCP Inspector on the dev hotel's MCP server, to call a tool by hand.
- `npm run ai:update`: refreshes this block and the skills from the server the plugin builds against.
  After moving to a newer server, read the plugin changelog (what changed for plugins: one file
  per change in `changelog/`, named by date, the newest last) and `upgrading.md` before fixing what
  no longer compiles.

### Looking things up

Look a member up before writing code a skill does not show, in this order:

1. **`search_docs` first.** When the `turbo` MCP server is connected (its tools are listed), call
   `search_docs` with what you need in words (`schedule a daily task`, `IRoomTurn`, `CommandResult`)
   and `read_doc` the section it names. It searches the guides and the skills of the server this
   plugin builds against, ranked, and answers in one call: do not grep the guides while it answers.
2. **Then the guides folder.** Only when the `turbo` server is not connected (no such tool, or it
   refuses the token): the same guides are Markdown files in `$TurboBin/plugin-sdk/docs/` (the
   folder under "This plugin" below); search them for the member's name.

The dev hotel's other tools, when connected:

- `plugin_errors`: the plugin's state on the dev hotel with each compile error, refused call (TPP)
  and client build error by file and line, and the plugin's recent warnings.
- `client_errors`: what players' clients reported of the plugin's client module (refused, threw, a
  template patch that matched nothing) and the console errors the client errors plugin recorded.
- `server_log`: the dev hotel's latest log lines and its last error (with the stack trace).
- `message_schema`: a plugin message's or a core packet's header and fields.
- `plugin_tables`: the plugin's tables and columns as the database has them; `plugin_query` reads
  its rows (one `SELECT` over the plugin's own tables).

Never invent an API: if a member is not in a skill or a guide, search for it, then `npm run check`.
