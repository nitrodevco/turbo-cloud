# Extending Turbo and Nitro: the framework model

Turbo (server, C#/Orleans) and Nitro (client, TypeScript/React/Pixi) are two programs in two
languages. To someone building a feature they should feel like one framework: one package, one set of
conventions, one place to look things up. This document says how they fit together, which extension
primitives exist, which are missing, and the order we are filling them in. It is the reference for
anyone changing the extension surface; the per-feature guides it links stay the detail.

The benchmark is not how many hooks there are. It is how little of either program's internals a
developer needs to build something substantial, correctly, and keep it working across releases.

## 1. One add-on, one package (what already holds)

A **plugin** is the package. One folder, installed, updated, rolled back and removed as a unit
(`docs/admin-panel.md`, Plugins):

```
plugins/skills/
  manifest.json                 key, version, dependencies, table prefix, API version
  Skills.dll                    server half: services, handlers, commands, listeners, migrations
  wwwroot/                      client half and its files, served at /gamedata/plugins/skills/
    skills.js                   a client module (docs/client-modules.md)
    template-patches.json       changes to existing windows, as data (§4.5)
    icons/...
  gamedata/
    external_texts.txt          its words, editable by staff
    external_variables.json     its client settings
```

Turbo is the host for both halves: it loads the assembly, serves `wwwroot/`, seeds the texts and
variables it owns, tells clients which modules to run, and takes all of it away when the plugin goes.
Nitro runs what Turbo names and gives modules a versioned API. That split is the Laravel-and-Inertia
shape: the server owns routing, data, auth and packaging; the client owns rendering, and a shared
contract joins them. We keep it. We do **not** add a second package format or a second manifest.

What already works (each with its guide):

| Need | How | Guide |
| --- | --- | --- |
| Services, DI, settings with panel forms | `TurboPlugin.Services`, `[PluginSettings]` classes | `settings.md` |
| Everyday hotel actions (players, rooms, wallet, furni, badges, catalog, notices) and a fake for tests | `IHotel`, `Turbo.Testing.FakeHotel` | `hotel-api.md` |
| Own tables and migrations | `PluginDbContextBase` (found and migrated as the plugin loads) | `database.md` |
| Packet handlers and packet middleware | `IMessageHandler<T>`, `IMessageBehavior<T>` | `plugin-packets.md` |
| Commands | `ICommand<TArgs>` | `commands.md` |
| Furni and wired logic | `FurniLogic` + `[FurniLogic]`, `WiredEffect`/`WiredTrigger<TEvent>`/`WiredCondition`/`WiredSelector` + `[WiredBox]`, `IHotel.Wired` (contracts only) | `plugin-wired.md` |
| Observe rooms | `IRoomEventListener` | `plugin-room-events.md` |
| Games | `IRoomGame`, `IGameAccessPolicy` | `plugin-games.md` |
| Permissions | `IPermissionNodeSource`, `[RequiresPermission]` | `permissions.md` |
| Admin pages | `IPluginAdmin` (schema-driven, no code in the panel) | `admin-panel.md` |
| Client code | `wwwroot/<key>.js`, client `ClientModuleApi` v1 | `client-modules.md` |
| Install, update, rollback, uninstall with data choice | `IPluginAdministration`, `IPluginDataOwner` | `admin-panel.md` |

## 2. What is wrong with it today

Found by building a complete hotel's features as plugins only, then reading both code bases end to end (2026-10-23). Ranked by how much internal knowledge
each forces on an author.

1. **Changing an existing window means shipping a copy of its whole library.** A window's colour,
   a hidden element or a moved button is a rebuilt `.nitro` bundle named in
   `asset.bundles.templates.overrides`. The copy is frozen at the release it was made from, and when
   two plugins name one library the first plugin's wins with no warning
   (`ExternalVariablesFile.Merge`). Two add-ons cannot both change the friends window.
2. **Nothing can be added inside an existing window.** Modules add whole windows, toolbar icons and
   me menu entries; anything inside a core window needs a core change. (A decorate-callback prototype
   exists outside main; it runs per render, clones and stringifies the template each time, and lets a
   module overwrite core's own bindings.)
3. **Plugin packets are numbered by hand on both sides.** Headers from a shared range, a C#
   serializer and a JS parser written separately, no schema, no collision check for outgoing
   headers between plugins.
4. **Modules reach into private client state.** `api.libs.stores` hands out writable zustand stores
   with internal field names; window names and link routes are the control surface.
5. **The client API is unversioned in practice.** Members were added under `version: 1`; modules
   feature-detect. Nothing is published with types.
6. **Domain actions cannot be vetoed or adjusted.** Only chat, commands, game access and incoming
   packets have hooks. No purchase, room entry or placement policy; trade has one host service
   (`IPlayerTradePolicy`, §4.2).
7. **A plugin cannot take part in host multi-registrations** (`IAchievementFactListener`,
   `IIncomeRewardSource`, word filter, name/figure policy): its container is separate and
   `IHostServices` only resolves.
8. **Lifecycle edges**: every rescan reloads every plugin; one plugin's DI failure could stop the
   host; a failed load could leave registrations behind; `MinVersion` is ignored; exports are never
   unbound; a plugin overriding a core logic key leaves no logic behind on unload. (The first three
   are being fixed in the plugin isolation work.)
9. **A plugin cannot serve players over HTTP.** It has static `wwwroot/` files and the staff-only
   admin API (`/api/plugins/{key}/api/...`, panel sign-in). Anything a player's client or a hotel's
   site asks a plugin for over the web - rare values, toplists, profiles, news, a shop page, a game's
   leaderboard - needs the plugin to host its own web server beside Turbo's, with its own auth.
   (Closed by plugin web routes, §4.8.)

## 3. The primitives

Every extension point is one of seven kinds. A new hook must be one of these, follow its rules, and
say which in its guide. One kind per problem keeps the surface learnable.

| Kind | Use it to | May change the outcome? | Runs |
| --- | --- | --- | --- |
| **Event** | observe something that happened (`PurchaseCompletedEvent`, `PlayerLoggedInEvent`, `PlayerLoginFailedEvent`, `PlayerRegisteredEvent`, `PlayerRegistrationFailedEvent`, `PlayerIdentityLinkedEvent`, `RoomEnteredEvent`, `HotelAvailabilityChangedEvent`: `docs/plugin-policies.md`) | no | after the fact |
| **Pipeline / policy** | allow, refuse or adjust an action before it commits (`IPurchasePolicy` incl. room ads, `IRoomEntryPolicy`, `IPlacementPolicy`, `IRegistrationPolicy`: `docs/plugin-policies.md`) | yes, explicitly | before commit, in the owner's context |
| **Service contract** | replace or add an implementation of a named contract | yes, the whole behaviour | wherever the contract is called |
| **Registry** | add a new thing of a known kind (command, logic, game, message, web route) | n/a | as registered |
| **Template patch** | change or extend an existing window, as data | presentation only | where the template is read |
| **Theme layer** | change how everything looks (frames, fonts, colours, art) | presentation only | at load |
| **Message** | talk between a plugin's client and server halves | n/a | network |

Rules every kind follows:

- **Ownership.** Everything registered is recorded against the plugin that registered it, so the
  host can list it, report conflicts by name and take it away. Elements a patch adds are named
  `<owner>:<name>`; windows are `<owner>/<id>`.
- **Deterministic order.** Explicit `order` first, then plugin key, then registration order. Never
  load-timing.
- **Conflicts are visible.** Two owners claiming one exclusive thing (a property, a command name,
  a header) is either refused at load or kept deterministic and reported - never silently decided.
- **Cleanup is automatic.** Whatever goes through a registry is removed when its plugin stops,
  whether or not the plugin disposed it.
- **Errors are isolated.** One plugin's failure fails that plugin (load, listener, module, window),
  logged with its key; the host and the other plugins carry on.
- **Execution context is part of the contract.** Each hook's guide says where it runs. In-grain
  hooks (room listeners, chat events) must not block or call back into their grain; slow work goes
  to a post-commit event (§4.1).
- **Public means contract.** Only `Turbo.Contracts`, `Turbo.Primitives` and the client's
  `ClientModuleApi` are API. A type an extension needs that lives in an implementation project
  (wired base classes in `Turbo.Rooms`) is a gap to close, not something to depend on.

## 4. The model, kind by kind

### 4.1 Events: two tiers

Keep `IRoomEventListener` as the cheap in-turn observer (it must stay fast). Add a **post-commit
domain event** tier for business features (XP, quests, analytics, notifications): published after
the state change is saved, carrying a stable event id for idempotency, delivered off the grain's
turn. The asset movement outbox already has these semantics; the new tier generalises it rather
than inventing a second mechanism.

The first post-commit events are on the event bus: `DomainEvent` (an `EventId` for idempotency and
`OccurredAtUtc`) published by `EventSystem.PublishCommitted` once the change is saved, on the thread
pool and not awaited, never for a refused action (save the two that tell of a refusal); a plugin's
`IEventHandler<T>` hears them. `PurchaseCompletedEvent` (room ads included), `PlayerLoggedInEvent`,
`PlayerLoginFailedEvent`, `PlayerRegisteredEvent`, `PlayerRegistrationFailedEvent`,
`PlayerIdentityLinkedEvent`, `RoomEnteredEvent` and `HotelAvailabilityChangedEvent` (the hotel
went into maintenance or out of it, a shutdown was scheduled; `IHotel.Status` reads the state) are
raised today; the login and registration
ones carry the client's address, personal data their text leaves out (`docs/plugin-policies.md`,
"Addresses"). They are delivered in memory: one raised as a server
stops is not kept, as the asset movement outbox's are.

### 4.2 Pipelines and policies

`IMessageBehavior<T>` (packets) and `IEventBehavior<T>` exist. Packet middleware is the wrong level
for game rules: it sees wire messages, not decisions. Add **typed policies** for the decisions hotels
actually customise - trade open/accept, purchase, room entry, furni placement, name/figure change -
each called by the owning grain before it commits, ordered, able to refuse with a reason the client
shows. Official behaviour stays the default implementation; a policy composes over it.

Trade has the first of them, as a single service: `IPlayerTradePolicy` answers what a player may do
in a trade (`TradeStandingType`: locked, no pass, receive only, full) by the `trade` permission and
the trading pass. Trades ask it of both players before they open and pick the refusal from it
(`openfail.3`, `trade.trading_perk`, `openfail.4`); wired chests ask it before they move anything,
and the marketplace before a sale (locked: no trading privilege; no pass or receive only: no
trading pass).
A plugin replaces it by registering its own, which changes all three (§4.3).

Purchase (room ads included), room entry, placement (floor plan saves included) and registration
have plugin policies: `IPurchasePolicy`, `IRoomEntryPolicy`, `IPlacementPolicy` and
`IRegistrationPolicy` (asked before an account is made, with its name, look and address), found in a plugin's assembly as it
loads and removed as it unloads, run in `Order`, then plugin key. A purchase's policies compose
(each takes the decision of the ones before, the offer's own price first); for the others the first
refusal is the answer. Two plugins with a policy of one kind both run and are reported as a conflict
(logged, and listed on each plugin in the admin API). With none the hotel decides as it always did.
`docs/plugin-policies.md`.

### 4.3 Service contracts

A host service marked `[ReplaceableService]` is replaced by a plugin that registers its own in its
services (`docs/plugin-services.md`): `ICampaignCalendarService`, `IPlayerTradePolicy` and
`IGameAccessPolicy` today. The host registers each through a slot (`AddReplaceable`), and what its
container gives for the contract forwards each call to the implementation in use, so core's
handlers, the login, the admin API and other plugins call the plugin's while it is live, though they
took the contract before it loaded, and nothing keeps a replaced one. The slot is a stack, as the
furni logic override (§2.8) showed it must be: the last plugin loaded is used, an overlap is logged
as a conflict and listed in the inspector (kind `service`), and on unload the one under it is back,
another plugin's or the host's. A replacement adds to the one it replaced through
`IServiceSlot<T>.Under(this)`. A host service a plugin registers that is not replaceable stays in the
plugin's container, and the load logs it. Adding to host multi-registrations
(`IAchievementFactListener`, `IIncomeRewardSource`, §2.7) is still to come.

The contract a plugin *calls* is the hotel API (`docs/hotel-api.md`): `IHotel` with `Players`,
`Rooms`, `Wallet`, `Inventory`, `Catalog`, `Notify` and `Clock`, about thirty-five methods, each marked
with where it may be called from (`[HotelCall]`: anywhere, not from the grain it asks, or off the
grain because it reads the database). Reads are query methods with limits (a toplist, circulation,
a name search), never a `DbSet`. Every plugin's container has it, and `Turbo.Testing.FakeHotel` is
the same API in memory with assertions, kept equal to the host's by one set of contract scenarios
run on both. It replaces the per-plugin adapter over grains and tables that every tested plugin
used to write. It is DI only, never a static facade: plugins load in their own contexts.

### 4.4 Registries

The assembly scan (commands, logic, listeners, games, permission nodes, wired variables) is the
Laravel auto-discovery equivalent and stays the default. New registrations follow it: implement the
public interface, get found, get removed on unload. Scheduled tasks are one (`IScheduledTask` with a
cron or interval, run once per cluster, or on every server with `PerServer = true`:
`docs/plugin-scheduler.md`).

### 4.5 Template patches (client)

Nitro draws windows from Habbo's own window layouts (`TemplateElement` trees in each library's
`templates.json`). The element names in those layouts are what the window's own code addresses
(`findChildByName`), so they are the stable identifiers a patch uses too: an author targets
`habbo-friend-list-com/friend_list_xml`, element `search`, exactly as the client's code does.

A patch is data: `set` layout properties (colour, style, caption, visibility, rect, vars, layout
params), `remove` an element, `insert` a new one before/after/inside another, or `move` one there
keeping its name (so the window's code still reaches it). Patches from every plugin apply where the
client reads a template, in a deterministic order, with a report of conflicts (two owners setting one
property), of targets a client release no longer has, and of extension code that threw. A theme
recolours a window and an add-on adds a button to the same window, and neither ships a copy of the
library.

Client modules patch through `api.templates.patch(id, ops)`; a plugin with no code ships
`wwwroot/template-patches.json` and the host lists it for clients (`templates.patches`). A module
can also: give ops and bindings as a function of the window's read-only context (the infostand's
user id, figure, whether it is the own user - each window's keys are documented client side), cached
per context rather than rebuilt per render; `bind` an element the window's code binds itself with
`{ override: true }`, its value winning for the fields it sets and reported as a conflict; and run a
post-layout hook after the window's own (`api.templates.arrange`), isolated from errors and removed
when the module stops. Client detail: nitro-next `docs/template-patches.md`; worked examples in
`docs/examples/template-patches`.

### 4.6 Theme layers

A theme is a plugin whose contribution is presentation: template patches, text styles, font faces,
frame art. Each is a merged variable a plugin brings its entries of (in its
`gamedata/external_variables.json`; never given to the hotel, the first plugin's entry used where two
name one, the hotel's own entries over all, collisions logged):

| Variable | Entries |
| --- | --- |
| `asset.bundles.templates.overrides` | a whole library's bundle url, for what nothing finer expresses (new art sheets) |
| `templates.patches` | a patch file's url per plugin - its `wwwroot/template-patches.json`, listed without it naming it |
| `frame.overrides` | `{ "<frame id or window name>": { "variant", "color" } }`: the style of a window drawn without a template (the wired setup window) |
| `asset.overlays` | `{ "<library>/<asset>": "<url>" }`: one bitmap of a library - one frame's art in the theme sheet (`theme-...`), one icon. A plugin names a file of its `wwwroot/` by its path (`"frame.png"`); clients are given its address by hash, and one that is not there is logged and left out |

So a theme replaces the one frame or icon it changes rather than shipping a copy of the window
manager library. Next: text-style sources per plugin.

### 4.7 Messages

Replace hand-numbered packets with **named, schema-described messages**: the plugin declares a
message once in C# (a record and a name); the host assigns headers from the plugin range at load,
refuses clashes, and sends clients the name -> header map and field schema as part of the module
list. The client side reads and writes by name (`api.messages.on('skills.levelled', ...)`), decoded
by the schema it was sent, and a generated `.d.ts` gives the author types. No header coordination,
no second serializer, and a mismatch between halves is detected, not misread.

**Implemented** (`client.messages`, header 30010): `[PluginMessage]` records and
`IPluginMessageHandler<T>` in `Turbo.Primitives.Plugins.Messages`, `api.messages.on/send` in the
client and the SDK's `ClientModuleMessageOf<typeof schema>`; see `docs/plugin-messages.md`. The map
is its own packet, sent before the module list; a `.d.ts` generator is still to come.

### 4.8 Web routes (a registry)

**Implemented**: `docs/plugin-web-routes.md`.

A plugin registers **player-facing HTTP routes** the way it registers commands: a public class
implementing `IPluginWebRoute` (`Turbo.Contracts.Plugins.Web`) is found by the scan and removed on
unload. The host owns everything around the handler, as the admin API already does for staff:

- **Address**: `/api/plugins/{key}/...` on the hotel's public web host (beside the gamedata host and
  the site API), so the reverse proxy that serves the hotel serves plugins too; a plugin cannot
  claim another's prefix or a core path.
- **Who is asking**: the hotel's own player session - the client's token (given over the game
  socket, `web.session`) or the site's session cookie, resolved by the host to a `PlayerId` - or
  anonymous where the route says so. Permission nodes (`[RequiresPermission]`) work as on packet
  handlers.
- **Limits**: body and time caps, rate limits per player and per address, CORS from the hotel's own
  site settings (`Turbo:Web:PluginRoutes`); errors are `{"error": ...}` answers with the plugin's
  key in the log.
- **Lifecycle**: routes go live after the plugin starts and stop answering (404) the moment it
  starts to unload; while it reloads they answer 503.

The client reaches them through the module API (`api.http.get('rares/values')`, the session
attached by the client), so a module never handles tokens itself.

Beside the routes, a **play policy** (`IPlayPolicy`, a policy of §4.2) lets a plugin allow or refuse a
player asking to play - the site's Play and the client's login - with a reason and an address to send
them to; with none, everyone plays as before (`docs/plugin-web-routes.md` §3). Both ask with the
address the player came from (the client's through `Turbo:Networking:TrustedProxies`). A plugin
links a Discord account it has proven to an existing player through `IHotel.Players.LinkIdentityAsync`
(`docs/hotel-api.md`, "Identity links"), and the site then signs that player in.

## 5. Stability

- **API versions**: `PluginApi.VERSION` (server) and the client API version are the contract
  numbers. Adding is minor; removing or changing meaning bumps them, with the previous version kept
  for one release and deprecations logged against the plugin using them.
- **Compatibility checks**: example plugins are compiled and loaded by the test suite
  (`HelloExamplePluginTests`); every new primitive gets an example there. Template patch reports name
  targets a new client release removed, so a release can be checked against installed plugins
  before it ships.
- **Trust**: plugins run in-process with full trust (server assembly, client JS in the page). The
  manifest's declarations are for compatibility and review, not a sandbox. Data plugins (patches,
  styles, art; no `AssemblyFile`) are the safe kind: they run no code on either side
  (`docs/plugin-data.md`).

## 6. Roadmap

Each step is a bounded, separately verifiable change. Later steps depend on earlier ones only where
noted.

1. **Template patches** (client engine + module API + host listing of `template-patches.json`;
   conflict and unmatched reports).
2. **Plugin lifecycle isolation** (failure isolation, rollback of a failed load, reload only what
   changed). A plugin's lifetime is the host's to give: `HotelReadyAsync` once the hotel is up (and
   after a reload), `ctx.Stopping` cancelled as it starts to stop, and the services it registers
   built over the host's (`docs/plugin-loading.md`).
3. **Named messages** (§4.7). Removes header coordination; needs nothing above. Done (`docs/plugin-messages.md`).
4. **Plugin web routes for players** (§4.8): the hotel's session and permissions on plugin HTTP
   endpoints, `api.http` in the client. Unlocks site/API features a hotel today runs as a separate
   web app beside the emulator (values, toplists, profiles, news, shop pages). Done, with play
   policies: `docs/plugin-web-routes.md`.
5. **Extension inspector**: one host endpoint and panel page listing every plugin's contributions
   (modules, patches, overrides, messages, commands, listeners, settings) with conflicts and
   failures; the client's patch report feeds it. Done: every registry notes what a plugin's load
   registers in its container (`IPluginContributionLog`, `services.Contributes(kind, name)`, so a
   registry a plugin adds for others lists too), `GET /api/plugins/{key}/contributions` lists it
   with the protocol it added, names another live plugin registers too and what clients report
   (`TurboClientExtensionReportMessage`, `docs/client-modules.md` §2); the panel's plugin row has
   Contributions and Client reports tabs. A refused client module is logged naming
   `client.modules.hosts`. In the client, staff with `client.inspector` alt-click a window element
   for its template key, the patches on it and a patch to copy (nitro-next
   `docs/template-patches.md`); a development build has it only after `NitroInspector()` in the
   browser console (off by default, remembered in that browser until `NitroInspector(false)`), so an
   alt-click presses what it always did.
6. **Typed client SDK**: `ClientModuleApi` published as types with `defineModule`, a narrow read-only
   store API in place of raw stores, namespaced links and layouts. The types, `defineModule`,
   `useApi` and a bundler preset are nitro-next's `packages/client-module-sdk`, kept equal to the
   client's API by its type check. API 1.1 adds the read-only state (`api.state`: own user, club,
   purse, room, permission checks) in place of `libs.stores` (deprecated), room events
   (`api.events`), buttons in the avatar menus and the furni infostand (`api.contextMenu`) and
   client chat commands (`api.chatCommands`); namespaced links and layouts are next.
7. **Policies** for trade, purchase, room entry and placement (§4.2), then post-commit domain events
   (§4.1). Purchase (room ads included), room entry, placement and registration done, with
   `npm run make policy <kind>`, and seven post-commit events (`docs/plugin-policies.md`); trade's
   policy is a replaceable host service (`docs/plugin-services.md`).
8. **Data-only plugins** (themes without an assembly) and per-asset overlays (§4.6). Done: a
   manifest with no `AssemblyFile` loads its files only, `theme.json` style properties reach
   patches as `${style:name}` with staff's values from the panel, staff's own patches live in the
   hotel's data plugin, and the panel packages either as a data plugin (`docs/plugin-data.md`).
9. **Scaffolding and dev loop**: `dotnet new turbo-plugin` generating both halves and their tests,
   one command that rebuilds and hot-reloads both while a player is online, one that packages the
   zip the panel installs: `templates/turbo-plugin` (`docs/client-modules.md`, "Start from the
   template"); `PluginTemplateTests` keeps it building against the server. Named messages and
   `npm run make` are in it (step 10).
10. **Conventions over boilerplate**: a `TurboPlugin` base that reads key and version from the
    manifest, registers `wwwroot/<key>.js` and `[PluginSettings]` classes by itself and gives
    plugin databases their design-time factory; an assembly without a plugin class loads as one.
    `npm run make <kind> <name>` in the scaffold writes a command, message, furni logic, listener,
    setting, web route, window, template patch or migration in the shape the scan finds. Done
    (`docs/plugin-loading.md`, `docs/client-modules.md`); scheduled tasks and policies get their
    `make` kinds with their own steps. Furni logic is a contract since step 12.
11. **Scheduled tasks** (a registry kind, §4.4): `IScheduledTask` with a cron or interval, run once
    per cluster, never overlapping, cancelled and removed on unload, listed in the panel with last
    and next run and Run now; `npm run make schedule <Name>`. Done (`docs/plugin-scheduler.md`);
    core's own periodic jobs may move onto it later.
12. **Wired kit** (§3, "Public means contract"): furni logic and wired boxes (effects, triggers,
    conditions, selectors) built against `Turbo.Contracts` alone, hosted by core's own kinds so a
    plugin box keeps core's checks, flashes, limits and stack rules; plugin events fire its
    triggers (`IHotel.Wired.FireAsync`, or `FireWiredAsync` from furni logic in the room);
    `npm run make wired <kind> <name>`. Done (`docs/plugin-wired.md`). Add-ons are core's (they
    change how a stack runs), and a box opens an official editor: the client has no editor of a
    plugin's own.

10. **Hotel API** (§4.3): `IHotel` for the everyday actions, `FakeHotel` for plugins' tests, contract
    tests on both. Done (`docs/hotel-api.md`); members are added when two plugins need the same one.

**How we know it works** (run after steps 6 and 9, then every release):
- **The stranger test**: an author with no knowledge of either code base - in practice an agent given
  only these docs, the SDK and the scaffold - builds a small real add-on (a window, a message, a
  setting, a patch on an existing window). Every time it has to read core source or gets stuck is a
  framework bug, logged and fixed before the next run.
- **Figures per release**: minutes from `dotnet new` to a working feature on a hotel, lines of
  boilerplate in a generated plugin, seconds for each half to hot reload, and the number of internal
  names an author must know (target: none).
