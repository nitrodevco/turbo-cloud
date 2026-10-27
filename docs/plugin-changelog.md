# Plugin Changelog

- [Introduction](#introduction)
- [2026-10-27](#2026-10-27)

## Introduction

Every change that alters what a plugin author writes, sees or gets: the contracts (`Turbo.Contracts`,
`Turbo.Primitives`, `Turbo.Database`), hooks and policies, how plugins load, build and install, the
template, and the client module API (nitro-next). Newest first. To bring a plugin up to date step by
step, read `docs/upgrading.md`.

Each entry says what changed, who it affects, whether it **breaks plugins** (a plugin that built
and ran before stops compiling, stops loading or behaves differently without a change of its own),
the action to take, and the pull request.

**Adding an entry.** A pull request that changes what a plugin author writes or sees adds its entry
here, at the top of today's date, in the same pull request; the pull request template's "Plugin
authors" section asks for it. A client module API change in nitro-next gets its entry here with the
turbo-cloud pull request it goes with, or in a small docs pull request of its own. A change no plugin
can notice (internals, core's own behaviour, tests) has no entry.

## 2026-10-27

### Turbo Boost: Cursor, Copilot and Junie; conventions; an MCP tool recipe; the Inspector

- **What**: `npm run ai` asks which agents to set up (or `--agents claude,codex,cursor,copilot,junie|all`)
  and writes each one's own instructions file, skill folder and MCP config (`.cursor/rules/turbo-boost.mdc`,
  `.github/copilot-instructions.md` and `.vscode/mcp.json`, `.junie/guidelines.md`,
  `.codex/config.toml`), each keeping what the author wrote. The guideline has the assistant call
  `search_docs` first. A new skill, `turbo-infer-conventions`, records a plugin's own conventions as
  rules once the author approves them (`npm run ai:rule ... --seen <files>`). `npm run make mcp-tool
  <Name>` writes a tool and its `McpToolTester` test; `npm run mcp:inspect` opens the MCP Inspector
  on the dev hotel's MCP server. The skills' TypeScript blocks are type-checked.
- **Who**: plugin authors who work with an AI assistant.
- **Breaks plugins**: no. Without `--agents` and outside a terminal, `npm run ai` now sets up the
  agents whose folders the plugin has (else Claude Code and Codex, as before).
- **Action**: a plugin made from an earlier template copies `tools/ai.mjs`, `tools/make.mjs`,
  `tools/mcp-inspect.mjs` and the `mcp:inspect` script of `package.json` from the template;
  `docs/turbo-boost.md`.
- [turbo-cloud#477](https://github.com/nitrodevco/turbo-cloud/pull/477)

### Human approval: actions that wait for a staff member's yes

- **What**: `IApprovals` (`Turbo.Contracts.Approvals`) saves a request for an action (kind,
  summary, arguments, who asked, the node an approver needs, an expiry); a staff member holding the
  node approves or denies it in the panel's new Approvals inbox, never their own; on approval the
  `ApprovalHandler<TArgs>` of its kind (a public class in a plugin, found as it loads) carries it
  out once. `ApprovalRequestedEvent`, `ApprovalDecidedEvent` and `ApprovalExecutedEvent` tell of
  it. An MCP tool with `RequiresApproval = true` answers `McpToolCallStatus.PendingApproval` with the
  request's id and runs on approval. New nodes `admin.approvals.view` and `approvals.decide`.
- **Who**: plugins and AI agents whose actions a person should approve; MCP tools that change the
  hotel; the MCP server plugin (a new call status, not an error).
- **Breaks plugins**: no.
- **Action**: none; to use it, read `docs/approvals.md`.
- [turbo-cloud#475](https://github.com/nitrodevco/turbo-cloud/pull/475)

### The documentation has a sidebar and a website

- **What**: `docs/documentation.md` orders every page under a sidebar, and each page opens with a
  table of contents and an Introduction. New guides: Installation, Configuration, Directory
  Structure, Deployment, Packet Lifecycle, Grains & Orleans, Errors & Logging, Furni Logic & Room
  Turns, Testing. `docs-site/` builds them, with nitro-next's guides, into a searchable site with
  `llms.txt` and a markdown copy of every page.
- **Who**: everyone reading the plugin guides.
- **Breaks plugins**: no.
- **Action**: none; start at `docs/documentation.md`.
- [turbo-cloud#474](https://github.com/nitrodevco/turbo-cloud/pull/474)

### A test kit: test a plugin in a hotel that loads it as the server does

- **What**: `Turbo.Testing.TestHotel` loads a plugin from its sources through the server's compiler
  and policy (one that would not compile or is refused fails with the server's reasons), registers
  what it declares with the server's own registries, and runs it as the test's build of the same
  sources. Tests then play the game: players who say commands and send named messages, purchases
  asked of its policies, furni running its logic, scheduled tasks on a clock the test moves,
  settings, web and admin routes, its tables in in-memory SQLite, and assertions in game terms
  (`bob.Should().HaveReceived<T>()`, `hotel.Purchases.Should().HaveRefused(...)`). A test project
  imports `plugin-sdk/Turbo.Plugin.Tests.props`. The template's tests use it, and `npm run make
  command|message` writes a test with the piece.
- **Who**: every plugin with tests.
- **Breaks plugins**: no. Test projects that reference `Turbo.Testing.dll` by hand keep working.
- **Action**: none; to move a test project onto the kit, `docs/plugin-testing.md`.
- [turbo-cloud#473](https://github.com/nitrodevco/turbo-cloud/pull/473)

### Turbo Boost: the AI kit for plugin authors

- **What**: the server ships `ai/` (a plugin guideline and ten Agent Skills, one per kind of plugin
  code) and its guides (`docs/**/*.md`) in `plugin-sdk/` beside its assemblies. The template's
  `npm run ai` / `ai:update` write the guideline into a `<turbo-boost>` block of `CLAUDE.md` and
  `AGENTS.md` (the rest is kept), install the skills in `.claude/skills` and `.agents/skills`
  (with those of the plugins in `Dependencies` that ship `ai/`), and add the dev hotel's MCP server
  to `.mcp.json`; `npm run ai:rule` records a project rule. `IServerLog`
  (`Turbo.Primitives.Logging`) holds the server's latest log entries for a plugin to read.
- **Who**: plugin authors who work with an AI assistant; plugins others build on, which can ship
  `ai/guidelines.md` and `ai/skills/`.
- **Breaks plugins**: no.
- **Action**: a plugin made from an earlier template copies `tools/ai.mjs` and the three `ai`
  scripts of `package.json` from the template, then runs `npm run ai`; `docs/turbo-boost.md`.
- [turbo-cloud#472](https://github.com/nitrodevco/turbo-cloud/pull/472)

### MCP tools: a plugin contribution kind for AI assistants

- **What**: a public `McpTool<TInput>` / `McpTool<TInput, TOutput>` (`Turbo.Contracts.Plugins.Mcp`)
  in a plugin is found as it loads and offered to AI assistants through an MCP server plugin
  (`IMcpTools`). Each needs `[RequiresPermission]` and a name no other live plugin's tool has, or
  the plugin fails its load; input and output schemas come from the types. `PlayerText` marks text
  players wrote. `Turbo.Testing.McpToolTester` calls tools in tests. `PluginWebResult.Accepted()`.
- **Who**: plugins that want to offer tools to assistants.
- **Breaks plugins**: no.
- **Action**: none; `docs/plugin-mcp-tools.md`.
- [turbo-cloud#469](https://github.com/nitrodevco/turbo-cloud/pull/469)

### A replacement reaches the service it replaced through the host

- **What**: a plugin that replaced a host service (`IServiceSlot<T>`) and asked the host for that
  same contract (`ctx.Host<T>()`, `IHostServices`) got itself back, so a call through it recursed
  until the stack overflowed and the server died. It now gets the implementation under its own (as
  `slot.Under(this)` gives), worked out at each call; other plugins and the hotel still get the one in
  use. A replacement whose constructor takes its own contract now fails its load with "A circular
  dependency was detected" instead of overflowing.
- **Who**: plugins that replace a host service (`docs/plugin-services.md`).
- **Breaks plugins**: no. A plugin that relied on `Host<T>()` returning itself crashed the server.
- **Action**: none; a plugin that took its own contract by constructor takes the one it replaces
  through `ctx.Host<T>()` or `Under(this)`.
- [turbo-cloud#468](https://github.com/nitrodevco/turbo-cloud/pull/468)

### A plugin message can carry a whole core packet

- **What**: a plugin message record may end with an `IComposer` field (schema type `"packet"`), a
  core packet sent after its other fields as a `PluginEnvelopeComposer`. Server to client only: a
  handler for such a message fails the plugin's load. In the client, the field reads as a
  `ClientModuleCarriedPacket` (`{ header, packet }`, parsed by the client's own parser) that a
  secondary room's `apply` takes as it is; `api.messages.send` refuses such a message. A room
  watch's envelope message must now have that field.
- **Who**: plugins relaying another room (portals, screens); client modules reading them.
- **Breaks plugins**: no (a watch envelope without the field, possible for an hour after
  turbo-cloud#464, is now refused with `ArgumentException`).
- **Action**: none; to relay, see `docs/plugin-messages.md`, "Carrying a core packet".
- [turbo-cloud#467](https://github.com/nitrodevco/turbo-cloud/pull/467),
  [nitro-next#254](https://github.com/nitrodevco/nitro-next/pull/254)

### Contracts for what plugins built on core's internals

- **What**: `IRoomTurn` (a room in its turn: `RoomUsers`, `Map`, `SayAsync` with `RoomSpeech`,
  `SetEffectAsync`, `StopWalkingAsync`, `SetRotationAsync`, bot figure and following, temporary
  furni), and `IFurniContext` is one. `FurniLogic` gains `OnDetachedAsync`, `UsagePolicy`,
  `Walkable`; `IFurniContext` gains `WalkToInOrderAsync`, `TileInFront`, `TilesBeside`,
  `StandsBeside`, `Schedule`/`CancelSchedule`. `RoomUser` gains `Rotation`, `IsWalking`, `IsIdle`,
  `EffectId`; `RoomFurni` gains `Rotation`, `Definition`. `ICommandRoom.SayAsync` and
  `FloorItemsAt`. `IRoomFurniWriter` (`Turbo.Database.Furniture`). `IPluginFiles` (the plugin's own
  folder, read only). `WiredVariableId.OfBox` (moved from `WiredVariableIdBuilder.CreateFromBoxId`,
  the same ids). `WiredBox.UsesTextPlaceholders`. `IWiredEffectContext.ShowMessageAsync` is now
  `SayAsync(user, text, RoomSpeech, ct)`.
- **Who**: every plugin that used `Turbo.Rooms` or `Turbo.Furniture` (no longer compilable since
  turbo-cloud#463); wired effects that showed a message; tests that build `RoomUser`/`RoomFurni`.
- **Breaks plugins**: yes: `ShowMessageAsync` is gone, and `RoomUser`/`RoomFurni` take more
  constructor arguments.
- **Action**: `docs/upgrading.md`, steps 7 and 8.
- [turbo-cloud#466](https://github.com/nitrodevco/turbo-cloud/pull/466)

### A room watch starts with the room as an entry draws it

- **What**: `IHotel.Rooms.WatchAsync` first sends the watcher the room's whole view (floor, furni,
  avatars, their states), then its stream, in order; a watch renewed after it lapsed starts with a
  new view.
- **Who**: plugins watching rooms.
- **Breaks plugins**: no.
- **Action**: a plugin that built the first frame itself can stop.
- [turbo-cloud#465](https://github.com/nitrodevco/turbo-cloud/pull/465)

### Client modules draw another room beside the room on screen

- **What**: `api.room.createSecondary(roomId, options)` draws a room fed with core packets
  (`apply`) beside the active one; `api.room.keepWhenLeft(options)` keeps the room being left as
  one; entering a drawn room adopts it. `onUnhandledPointer` events carry `secondaryTile`.
- **Who**: client modules.
- **Breaks plugins**: no.
- **Action**: none; nitro-next `docs/client-modules.md`, "Secondary rooms".
- [nitro-next#253](https://github.com/nitrodevco/nitro-next/pull/253)

### Holds and watches of rooms, and envelope packets

- **What**: `IHotel.Rooms.HoldAsync`/`ReleaseAsync` keep a room loaded and awake with no one in it
  (a lease, renewed by calling again); `WatchAsync`/`UnwatchAsync` send a player a room's packets
  inside the plugin's envelope message; `IFurniContext.WatchRoomAsync` and `ReadExtraDataJson`.
  Everything a plugin holds or watches ends when it unloads.
- **Who**: plugins showing one room in another.
- **Breaks plugins**: no.
- **Action**: none; `docs/plugin-room-watch.md`.
- [turbo-cloud#464](https://github.com/nitrodevco/turbo-cloud/pull/464)

### Plugins are compiled from their sources; no plugin assembly is loaded

- **What**: the server compiles each plugin's C# sources (manifest `Sources`, default
  `server/**/*.cs`) against the plugin surface only: the framework, `Turbo.Contracts`,
  `Turbo.Primitives`, `Turbo.Database` and the packages in `docs/plugin-loading.md`. Code that loads
  assemblies, calls native code, uses unsafe code, emits code, starts processes or uses the compiler
  is **Rejected** (`TPP001`-`TPP006`), and so is a folder holding an executable file. Compiles are
  kept, signed per server. The manifest loses `AssemblyFile` and `BuiltAgainst`. The server ships
  `plugin-sdk/Turbo.Plugin.props`, which a plugin's project imports to build exactly as the server
  compiles (`npm run check`). A GitHub install takes the tag's source archive, not a release asset,
  and the link's `Asset` field is now `Folder`.
- **Who**: every plugin with server code.
- **Breaks plugins**: yes: a plugin that ships an assembly is Rejected, and one using the server's
  internals no longer compiles.
- **Action**: `docs/upgrading.md`, all of it.
- [turbo-cloud#463](https://github.com/nitrodevco/turbo-cloud/pull/463)

### The server's copy of the client module SDK is pinned

- **What**: the SDK runtime the server builds modules with (`Turbo.Plugins/ClientBuild/sdk/`) is
  checked against the nitro-next commit it came from (`sdk-pins.json`).
- **Who**: no plugin; turbo-cloud contributors updating the copy (`scripts/client-module-sdk.mjs`).
- **Breaks plugins**: no.
- **Action**: none.
- [turbo-cloud#462](https://github.com/nitrodevco/turbo-cloud/pull/462)

### The server builds client modules from their TypeScript

- **What**: a plugin's module is built by the server from `client/src/index.tsx` (or `index.ts`)
  with esbuild, not minified, source map inline, and registered without code. Prebuilt JavaScript is
  neither registered nor served (`.js`, `.mjs`, `.cjs` in `wwwroot/` are refused).
  `IClientModuleRegistry.Register(key, entry)` takes a `.ts`/`.tsx` entry under `client/src/`. A
  module imports only its own files, the SDK, and the client's `react` and `zustand`; anything else
  fails the plugin with its `file:line:column`.
- **Who**: every plugin with a client module.
- **Breaks plugins**: yes: a module shipped as `wwwroot/<key>.js` is no longer served.
- **Action**: `docs/upgrading.md`, step 6.
- [turbo-cloud#461](https://github.com/nitrodevco/turbo-cloud/pull/461)

### Furni hooks, linked pairs and room arrival policies

- **What**: `IFurniContext` gains `ReadExtraData<T>`, `PairedItemId`, `GetRoomOfItemAsync`,
  `ObjectExtra`/`SetObjectExtraAsync`, `WalkToAsync` with an arrival callback, `StopWalkingAsync`,
  `SetRotationAsync`, `ShowStepAsync`, `ForwardToRoomAsync` (with an entry extension).
  `[FurniLogic(key, LinkedPair = true)]` makes a plugin's pair furni, whose forward passes the other
  half's room door as a teleporter does. `FurniLogic.OnMessageAsync` answers
  `IHotel.Rooms.InvokeFurniLogicAsync`. `IRoomArrivalPolicy` (`make policy room-arrival`) picks where
  an arriving avatar starts. `IsArrivingByTeleportAsync` is now `PassesRoomDoorAsync`.
- **Who**: plugins with furni logic; plugins that checked how a player arrived.
- **Breaks plugins**: yes, only for a plugin that called `IsArrivingByTeleportAsync`.
- **Action**: rename the call; `docs/plugin-wired.md` ("Furni logic", "Linked pairs", "Messages from
  your own code") and `docs/plugin-policies.md` for the rest.
- [turbo-cloud#460](https://github.com/nitrodevco/turbo-cloud/pull/460)

### Client module API: capabilities, core packet parsing, unused pointer events

- **What**: `api.capabilities.declare(name, version?)` asks the server for an extension of the
  plugin's while the module runs (`version`, `subscribe` read the answer);
  `api.packets.parseCore(header, body)` reads a core packet with the client's own parser;
  `api.room.onUnhandledPointer(listener)` hears pointer events over the room that the room did not
  use (return `true` to keep the room from acting on it).
- **Who**: client modules.
- **Breaks plugins**: no.
- **Action**: none; nitro-next `docs/client-modules.md`.
- [nitro-next#252](https://github.com/nitrodevco/nitro-next/pull/252)

### Every way a purchase is made asks the purchase policies, and a failing policy refuses

- **What**: `PurchaseRequest` gains `Kind` (`Catalog`, `ClubMembership`, `Gift`, `RoomAd`,
  `ClubGiftClaim`, `LimitedEdition`) and `ExtraParam`; club gift claims and limited edition entries
  now ask the policies too. `PurchaseDecision.Refuse(reason, error)` can show one of the hotel's
  purchase errors. A policy that throws now **refuses** the purchase (it was passed over).
  `PurchaseCompletedEvent` gains `Kind` and `ItemIds`.
- **Who**: plugins with a purchase policy or listening to purchases; tests that build either record.
- **Breaks plugins**: yes: a policy that throws now blocks purchases, it may now be asked for club
  gifts and limited editions, and `Kind` is required where the records are built.
- **Action**: make a policy that should not apply to a kind return `proposed` for it; pass `Kind` in
  tests. `docs/extensions.md`, "Deny or react to a purchase"; `docs/plugin-policies.md`.
- [turbo-cloud#459](https://github.com/nitrodevco/turbo-cloud/pull/459)

### Live reload of a theme's changes

- **What**: a change to a theme (saved in the panel, or a data plugin's file edited on disk) is sent
  to the players whose look it is part of, as it is saved (`turbo.theme.changed`), and the client
  draws it in place.
- **Who**: theme (data plugin) authors.
- **Breaks plugins**: no.
- **Action**: none; `docs/plugin-data.md`, "Live reload".
- [turbo-cloud#458](https://github.com/nitrodevco/turbo-cloud/pull/458),
  [nitro-next#251](https://github.com/nitrodevco/nitro-next/pull/251)

### Windows a theme defines as data

- **What**: `wwwroot/windows/<name>.json` in a plugin is a window of its own (template, button
  actions, me menu or toolbar entry), listed to clients in `templates.windows` and editable in the
  panel.
- **Who**: theme (data plugin) authors.
- **Breaks plugins**: no.
- **Action**: none; `docs/plugin-data.md`, "Windows of its own".
- [turbo-cloud#457](https://github.com/nitrodevco/turbo-cloud/pull/457),
  [nitro-next#249](https://github.com/nitrodevco/nitro-next/pull/249)

### A theme's texts draw over the hotel's

- **What**: a theme's `gamedata/external_texts.txt` is no longer seeded into the hotel's texts. Players
  drawing the theme get its texts over the hotel's (`text.overrides`), for every key staff never
  changed. Plugins that are not themes are seeded as before.
- **Who**: theme authors.
- **Breaks plugins**: yes, for a theme that brought texts of its own: every player had them, now only
  the players drawing the theme do.
- **Action**: none for a theme; `docs/plugin-data.md`, "A theme's texts".
- [turbo-cloud#456](https://github.com/nitrodevco/turbo-cloud/pull/456),
  [nitro-next#247](https://github.com/nitrodevco/nitro-next/pull/247)

### Who gets which theme

- **What**: each theme has a status (Live, Development, Off), one may be the default, and players
  may choose among those allowed. A newly installed theme is in Development: no player gets it until
  staff set it Live.
- **Who**: theme authors and the hotels installing their themes.
- **Breaks plugins**: no for themes already installed (they stay Live); a new install needs staff to
  set it Live.
- **Action**: say so in your theme's install notes; `docs/plugin-data.md`, "Who gets which theme".
- [turbo-cloud#455](https://github.com/nitrodevco/turbo-cloud/pull/455),
  [nitro-next#243](https://github.com/nitrodevco/nitro-next/pull/243)
