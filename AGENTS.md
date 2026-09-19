# Turbo Cloud AI Contract

This file is the canonical coding contract for AI-assisted changes in `turbo-cloud`.
Tool-specific instruction files should reference this file instead of duplicating rules.

## Foundational context
This repository targets the following core stack. When coding, prefer patterns compatible with these versions:
- .NET SDK `10.0.400` (from `global.json`)
- C# / BCL `net10.0`
- Orleans `10.2.2`
- EF Core `9.0.19` (held on 9.x: Pomelo `9.0.0` pins `Microsoft.EntityFrameworkCore.Relational` to `[9.0.0, 9.0.999]`)
- Pomelo MySQL provider `9.0.0`
- SuperSocket `2.1.0`

## Skills activation
Activate the relevant skill checklist before editing code in that domain:
- `handler-development`
  - Trigger: editing files under `Turbo.PacketHandlers/**` or message-to-composer orchestration logic.
  - Enforce: orchestration-only handlers, no DB queries, canonical grain access, no silent catches.
- `grain-development`
  - Trigger: editing files under `Turbo.*\\Grains\\**` or `Turbo.Primitives/**/Grains/*.cs`.
  - Enforce: keep ownership boundaries, lifecycle rules, and snapshot/state coherence.
  - Enforce: all rules in the **Orleans grain development rules** section below.
- `session-presence-routing`
  - Trigger: touching session gateway, presence flow, room routing, outbound composer fan-out.
  - Enforce: player outbound via `PlayerPresenceGrain.SendComposerAsync`; no direct handler socket sends.
- `message-contracts`
  - Trigger: editing `Turbo.Primitives/Messages/Incoming/**` or outgoing composer payload mappings.
  - Enforce: explicit mandatory fields, no placeholder payloads when source data exists.
- `revision-protocol`
  - Trigger: changes referencing `Revision<id>` packet mappings.
  - Enforce: edit `Turbo.Revisions/Revision<id>/**` in `turbo-cloud`.

## Priority order
1. Build and quality checks in repo files (`Directory.Build.props`, `Directory.Build.targets`, `.editorconfig`)
2. `CONTEXT.md` architecture and placement boundaries
3. Existing neighboring code conventions in the target folder
4. Tool-specific adapters (for example `.github/copilot-instructions.md`)

## Portable prompt contract
Use this request shape with any AI tool:
1. Goal:
2. Target files:
3. Required context files:
4. Invariants to preserve:
5. Forbidden changes:
6. Validation commands:
7. Output format:

Default output format:
- concise rationale
- file-by-file diff summary
- risks/assumptions
- exact validation command results

## Required standards
- Target framework/tooling: `.NET 10` pinned via `global.json`.
- Keep C# formatting compatible with repo quality gates (`dotnet csharpier check`, `dotnet format`).
- Follow `.editorconfig` naming/style preferences.
- Keep diffs focused and minimal; avoid unrelated refactors.
- Avoid introducing new dependencies unless required by the task.

### Type placement
- Every snapshot type lives in its own file under `Turbo.Primitives`, named after the type, in the
  folder for its domain (`Turbo.Primitives/<Domain>/Snapshots/<Name>Snapshot.cs`, for example
  `Turbo.Primitives/Rooms/Snapshots/RoomEventSnapshot.cs`).
- Never declare a snapshot inside another type's file (a composer, message, grain or service).
- The same applies to shared enums and DTOs: one public type per file, placed under the domain it
  belongs to, so other modules can use it without depending on the file that happened to need it
  first.
- A record stored as a JSON section of a furniture item extra data (`PetPackageData`,
  `PresentStorage`, `WiredRewardClaim`, ...) lives in `Turbo.Primitives/Furniture/ExtraData/`,
  namespace `Turbo.Primitives.Furniture.ExtraData`, next to the `FurnitureExtraDataSections`
  reader. It names its section in a `SECTION` constant when it owns one. Sections are read
  through `FurnitureExtraDataSections.Read`, which takes the logger so an unreadable section is
  logged with its name; do not deserialize a section by hand or keep its name in the logic
  class. Never put a section record directly under `Turbo.Primitives/Furniture/` or beside the
  logic that reads it. Client-facing key and
  state tables (`PresentData`, `MannequinData`, `DiceStates`) are not section records and stay
  under `Turbo.Primitives/Furniture/`.
- A database row becomes a snapshot through an extension method on the entity, in
  `Turbo.Database/Extensions/<Domain>EntityExtensions.cs` (`entity.ToSnapshot(...)`,
  `RoomEntity.ToInfoSnapshot(...)`). Whatever the row does not hold (owner name, grouped child
  ids, a resolved definition, "now") is a parameter, so the mapping stays a pure function with
  no provider or grain inside it. Do not write `new XSnapshot { Id = entity.Id, ... }` in a
  grain, provider or service: the second place that needs the same mapping will copy it and the
  two will drift. Two things stay inline: a column-trimmed projection inside a query
  (`.Select(x => new PlayerBadgeSnapshot { ... })`, so SQL fetches only those columns) and a
  snapshot built from grain state rather than from a row.
  When one snapshot record derives from another (`RoomSnapshot : RoomInfoSnapshot`), map the
  shared fields once: the derived record takes the base in a `[SetsRequiredMembers]` constructor
  that calls the record copy constructor (`: base(info)`), and its extension is
  `new(entity.ToInfoSnapshot(...)) { room-only fields }`. That attribute switches off the
  compiler's required-member check for that constructor, so keep it to the one mapping site and
  keep a parameterless constructor beside it for everything else.
- What a furni leads to is stored on the item, in the `RoomLinkerData` extra data section: a
  fixed `RoomId` for a room linker, the paired `ItemId` for a teleporter. There is no link table.
  A pair's room is never stored, because either half can be picked up and placed elsewhere;
  resolve it when needed (`RoomFurniModule.GetRoomIdOfItemAsync`). A dangling `ItemId` (the pair
  was deleted) simply leads nowhere.

### Constants and magic values
- Do not scatter hardcoded literals or `const` fields through implementation files. Give them a home:
  - a value the protocol or domain defines (message types, result codes, entry kinds) becomes an
    **enum** in `Turbo.Primitives/<Domain>/Enums/`, and the serializer casts it;
  - a set of related identifiers the client sends (search codes, cache keys) becomes one **shared
    static class** in `Turbo.Primitives/<Domain>/` (see `NavigatorSearchCodes`,
    `NavigatorListingKeys`);
  - anything an operator may want to tune (limits, lengths, timeouts, intervals, caps) becomes a
    **config option** on the module's config class, bound from `appsettings.json`.
- Keep an inline literal only where it is local and self-evident, and comment why.

## Behavioral rules for generated code
- Match local conventions in the files you touch.
- Prefer deterministic handlers/services with clear guard clauses.
- Preserve cancellation and async flow where it already exists.
- Handle failure paths explicitly; do not ship happy-path-only changes.
- Avoid dead code, unused allocations, and broad catch blocks that hide errors (see **Orleans grain development rules** for specifics).
- For revision compatibility work, prefer restoring/adding missing incoming message contracts in `Turbo.Primitives/Messages/Incoming/**` before mutating serializer/composer payload behavior.
- Do not alter serializer/composer behavior by replacing real payload writes with placeholder constants (for example, unconditional `WriteInteger(0)`) unless explicitly requested.
- If work references `Revision<id>` parsers/serializers, edit `Turbo.Revisions/Revision<id>/**` in `turbo-cloud`.

## Orleans grain development rules
These rules exist because every one of these mistakes has shipped and caused real issues.

### Never swallow exceptions silently
Every bare `catch { }` hides a real bug path. Always use `catch (Exception ex)` and log it.
If a cross-grain notification fails silently, state goes asymmetric and nobody knows why.
- **Required**: inject `ILogger<T>` into every grain that does cross-grain calls or DB work, typed
  by the grain interface (`ILogger<IPlayerGrain>`), and reach it from that grain's modules and
  systems rather than giving them their own.
- **Forbidden**: bare `catch { }`, `catch (Exception) { }` without logging, and `Console.WriteLine`
  in place of the logger.
- Log with structured templates and the identity the entry is about — `_logger.LogError(ex, "Failed
  to remove item {ItemId} from room {RoomId}", itemId, _state.RoomId)` — never string interpolation,
  so entries can be searched by id.
- Pick the level by who is at fault: `LogError` for a failure of ours, `LogWarning` for a rejected
  or impossible request (insufficient balance, unknown id), `LogDebug` inside per-tick loops where
  error-level logging would flood.
- `OnActivateAsync` hydration: catch, log with the grain key, then rethrow. Activation must still
  fail, but never namelessly.
- `OnDeactivateAsync`: isolate each step in its own try/catch and log. One failing step must never
  skip the ones after it — a failed flush still has to release the grain's registrations.

### Every grain has the same shape
A reader who knows one grain should be able to find their way in any other. `PlayerGrain`,
`PlayerNavigatorGrain` and `RoomPersistenceGrain` are the references.
- **Members, top to bottom:** injected dependencies (readonly), `_state`, modules, then runtime
  handles (timers, stream subscriptions, the session observer), then the key property, then the
  constructor, `OnActivateAsync`, `OnDeactivateAsync`, and the interface methods.
- **Constructor:** a classic constructor, never a primary one. Most grains need a body (the
  state is created with the grain key, modules take `this`), and a grain that captured
  primary-constructor parameters used them directly in its methods while its neighbours used
  `_fields`. One form everywhere ends that.
- **Parameter order:** `IDbContextFactory<TurboDbContext>`, `IOptions<...>` config, `IGrainFactory`,
  domain providers and services, `EventSystem`, and `ILogger` last. The logger is typed on the grain
  interface (`ILogger<IPlayerGrain>`), not the class. DI resolves by type, so the order is only
  for the reader; keep it anyway.
- **Live state:** everything the grain holds in memory about its domain (collections, snapshots,
  dirty flags, write buffers, counters) lives in `internal sealed class <GrainName>LiveState`, in
  `<GrainName>LiveState.cs` beside the grain, held as `private readonly <GrainName>LiveState _state`.
  Members are properties: `{ get; }` for collections, `{ get; set; }` for values, and a comment
  where the meaning is not obvious. Loose `_dictionary` fields on the grain are drift. What stays
  on the grain is what is not data: dependencies, config, modules, and handles that must be
  disposed (timers, stream subscriptions, observers).
- **The grain key** is read once, in the constructor, into the state
  (`_state = new() { PlayerId = this.GetPlayerId() }`, `RoomId = this.GetRoomId()`), and code reads
  `_state.PlayerId`. Do not call `this.GetPlayerId()` or `this.GetPrimaryKeyLong()` through the
  body, and never read a key through the wrong helper (the raffle grain read its series id with
  `GetPlayerId()`). A singleton grain's state carries no key; a stateless grain
  (`CatalogPurchaseGrain`) has no state class and reads its key where it needs it.
- **Timer fields** are named for what they drive (`_flushTimer`, `_dirtyItemsTimer`), never `_timer`.
- **A flush used in `OnDeactivateAsync` must be able to give up.** If a failed write re-queues its
  rows, a `while (pending > 0) await FlushAsync()` loop never ends while the database is down.
  Have the flush report whether it made progress and stop on the first failure.
- **Module files hold one concern.** A partial module file is named for what it covers
  (`RoomPetModule.Products.cs` is things used on a pet). Placement, pick-up and returning to the
  owner are the module's core and live in the main file, in the same place for sibling modules
  (`RoomPetModule.cs`, `RoomBotModule.cs`).

### Activate and deactivate the same way everywhere
- A grain that owns database-backed state hydrates it in `OnActivateAsync`, inside a try/catch that
  logs with the grain key and rethrows. Do not hydrate bare: a silent activation failure surfaces
  later as empty state.
- Lazy hydration is allowed only where eager loading would be wasteful (`InventoryGrain` loads
  furniture on first use, because the grain is also activated for cheap lookups). Say so in a
  comment on the class, so the exception reads as a decision rather than an omission.
- Every grain's class comment states how its state reaches the database: write-through
  (`PlayerWalletGrain`, `PlayerWardrobeGrain`) or buffered-and-flushed (`PlayerSettingsGrain`,
  `PlayerMessengerGrain`). This is what tells a reader whether a missing `OnDeactivateAsync` is
  correct.
- Buffered state is flushed on a timer *and* in `OnDeactivateAsync`, and the buffer is bounded by a
  configured limit so a database outage cannot grow it without end.
- A failed flush re-queues what it dropped (up to that limit) and logs; it never throws out of
  deactivation.
- Do not leave `OnActivateAsync`/`OnDeactivateAsync` overrides that only `return Task.CompletedTask`.
  An empty override says nothing the base class does not already do.

### Register timers one way
- Use the static callback form so the timer never captures the grain's fields or an outer token:
  `this.RegisterGrainTimer<object?>(static async (self, ct) => await ((TGrain)self!).TickAsync(ct), this, dueTime, period)`.
- The callback body belongs in a named method on the grain, not inline in the registration.
- Use the token the timer passes. Never capture the `CancellationToken` from `OnActivateAsync`: it
  covers activation only, and the timer outlives it.
- Keep the returned `IDisposable` in a field and dispose it in `OnDeactivateAsync`, before any final
  flush, so a tick cannot race deactivation.
- A tick that can fail catches, logs and keeps the schedule alive; one bad tick must not stop the
  grain from ticking again.

### Attribute every type that crosses a grain call
- Orleans deep-copies each argument and return value of a grain method. A type without
  `[GenerateSerializer]` throws `CodecNotFoundException` the first time it crosses a grain call —
  at runtime only; the build says nothing.
- New composer: `[GenerateSerializer, Immutable]` on the record and `[Id(n)]` on every member,
  numbered from zero and never renumbered afterwards. `Immutable` is correct for composers because
  they are built once and sent; it lets Orleans skip the copy.
- Same for snapshots and any DTO named in a grain interface. Events keep `[GenerateSerializer]`
  without `Immutable`, since handlers mutate them (`PlayerChatEvent` rewrites `Text`).
- A composer that is only ever sent from inside the grain that built it still needs this: the next
  caller to route it through `IPlayerPresenceGrain.SendComposerAsync` crosses a proxy.

### Declare grain implementations `internal sealed`
- A grain implementation is an implementation detail: only its interface is public. Declare it
  `internal sealed` (`internal sealed partial` when split across files) and keep the interface in
  `Turbo.Primitives/<Domain>/Grains/`.
- The exception is `RoomGrain`, which stays public, and the reason is on the class. Types discovered
  by `AssemblyExplorer` (`IRoomObjectLogic` implementations, `IWiredInternalVariable`
  implementations) must be public — it skips non-public types — and those name `RoomGrain` in their
  constructors and protected fields, so it cannot be narrower than they are.
- Packet handlers (`IMessageHandler<T>`) are discovered the same way and stay public for the same
  reason.
- Before narrowing any type's visibility, check whether it is reflection-discovered. Making one
  internal still compiles; it just registers nothing at startup, which no build or test catches.

### Grain method signatures
- Every asynchronous grain interface method takes a `CancellationToken ct` as its last parameter,
  including simple getters. Callers pass the token they were given, or `CancellationToken.None`
  for fire-and-forget calls.
- Take the grain key once, through `this.GetPlayerId()` / `this.GetRoomId()`, instead of casting
  `this.GetPrimaryKeyLong()` at each use site.

### Parallelize independent grain calls
When checking status on N grains (e.g. online status for a friend list), do not `await` each one in a `foreach`.
Grain calls to different grains can run concurrently with `Task.WhenAll`.
- Sequential = O(n) round-trips. Parallel = O(1) wall time.
- Apply everywhere: activation hydration, search results, batch accept/deny.

### Do not repeat identical grain calls in loops
If a grain method calls its own player's `GetSummaryAsync` inside a loop, hoist the call before the loop.
Same result every iteration = wasted round-trips.

### Batch DB operations
Do not loop `ExecuteDeleteAsync` per entity. Use a single `WHERE ... IN (...)` query.
Same for composer fan-out: collect all updates, send once.

### Use timer-based flush for housekeeping writes
Follow the `RoomPersistenceGrain` pattern: queue dirty state, flush with `RegisterGrainTimer` on interval, and flush on `OnDeactivateAsync`.
Do not issue per-event DB writes that block the grain turn.

### Do not hardcode limits in grains
Magic numbers like `Take(50)`, `Take(20)`, or `maxIgnoreCapacity = 100` are config options (see
**Constants and magic values**). A 10,000 user hotel needs different tuning than a 10 player dev
server.
- The grain that enforces a limit reads it: it takes `IOptions<TConfig>` of its module's config
  class in its constructor and keeps `.Value` in a field (`PlayerMessengerGrain` reads
  `PlayerConfig.MessengerNormalFriendLimit`, `BadgeLeaderboardGrain` reads `BadgeConfig`).
- A limit is not a parameter of a grain interface method and a handler does not read it from
  `IConfiguration` to pass along. This file used to say the opposite, and it lost: a limit that
  arrives as an argument is only as good as every caller, a plugin calling the grain skips it
  altogether, and the three handlers that still injected `IConfiguration` for it never read it.
- What does arrive as an argument is a number the *client* chose (a page size, a chunk index).
  The grain clamps it to the configured limit before using it
  (`BadgeLeaderboardGrain.GetLeaderboardAsync`).

### Use tracked deletes for atomicity
`ExecuteDeleteAsync` commits immediately and bypasses the EF change tracker.
If a delete + insert must succeed or fail together, use `FirstOrDefaultAsync` + `Remove` so both go through one `SaveChangesAsync`.

### Replace .Ignore() with a LogAndForget helper
Orleans `.Ignore()` makes cross-grain failures invisible. Use a `LogAndForget` extension that calls `ContinueWith(OnlyOnFaulted)` to log the exception.
Still fire-and-forget, but failures are visible in production logs.

### Bound session/history collections
Any in-memory collection that grows per-message (e.g. conversation history) must have a configurable cap.
Without a cap, long-running sessions leak memory.

### One grain per responsibility — isolate heavy I/O
Each major domain component should operate in its own grain. When a grain needs heavy I/O (DB writes, persistence flushes), delegate that work to a dedicated secondary grain so it does not block the primary grain's turn.
- Example: `RoomGrain` delegates furniture saves to `RoomPersistenceGrain`. The room grain stays responsive while persistence queues and flushes.
- Do not combine domain logic and persistence flushing in the same grain.
- The same holds for reads. A hotel-wide singleton that other grains await on their hot path (a
  directory) answers from memory; a query per request goes in a grain of its own. The badge
  leaderboards were first written into `BadgeDirectoryGrain`, where a grouped query over every
  badge row held up every badge list, profile and room entry in the hotel until it returned;
  they are now `BadgeLeaderboardGrain`.

### Use grain boundaries for thread safety
Orleans grains are single-threaded by design. Use this for concurrency-sensitive operations by giving each user their own grain for the operation.
- Example: each player gets a `PurchaseGrain` so catalog purchases are serialized per-player with no locks needed.
- Example: limited-edition items should use a dedicated grain (e.g. `LimitedItemGrain`) so concurrent buyers are safely serialized.
- Do not add manual locking (`lock`, `SemaphoreSlim`) inside grains — that fights the actor model.

### Grains orchestrate their own outbound communication
When grain state changes (e.g. wallet balance updates), the grain itself sends the snapshot to `PlayerPresenceGrain.SendComposerAsync`. The caller that triggered the change does not pass or send the composer — the grain owns that responsibility.
- **Correct**: handler calls `grain.UpdateWalletAsync(...)` → grain updates state → grain calls `PlayerPresenceGrain.SendComposerAsync(...)`.
- **Wrong**: handler calls `grain.UpdateWalletAsync(...)` → handler builds composer → handler sends composer to player.

### Do not mutate the database directly for grain-owned state
Grains may hold cached or in-memory state that will not reflect direct DB changes. All mutations to grain-owned data must go through the grain's methods, even when the player is offline.
- If a grain uses `[PersistentState]`, state is hydrated from the configured store (not DB) on activation. Direct DB edits will be overwritten by stale store data.
- Admin tools and external systems must call grain methods, not issue raw SQL/DB updates, for data that grains own.

## Room object logic rules
- Behaviour lives in a `[RoomObjectLogic("<type>")]` class under `Turbo.Rooms/Object/Logic/`; the
  type name must equal the `furniture_definitions.logic` value, or the provider falls back to
  `default_floor` and logs a warning. Never special-case a furniture type in a handler or module.
- A client action with its own packet (dice, dimmer preset, mannequin outfit, love-lock answer)
  is a `FurnitureInteraction` record under `Turbo.Primitives/Furniture/Interactions/` carrying
  that packet's payload, routed through the single `IRoomGrain.InteractWithItemAsync`; the logic
  pattern-matches it in `OnInteractAsync` and checks its own permissions with `HasRightsAsync` /
  `IsOwnerAsync`, refusing through `Reject(...)` so the refusal is logged with ids. A plain
  double-click stays on `OnUseAsync`. Do not add a grain method per furniture type.
- Replies that go to one player (a preset list, an unwrapped gift) are sent by the logic via
  `SendComposerToPlayerAsync` (the grain factory extension); room-wide changes go through
  `SetLegacyDataAsync` / `SetNumberDataAsync` /
  `SetStringDataAsync` / `SetMapDataAsync`, which persist and refresh in one step.
- The exact data shape each widget needs (dimmer `state,preset,effect,#RRGGBB,brightness`, toner
  `[state,h,s,l]`, love lock string array, trophy tab-separated) comes from the Flash client's
  logic classes; the constants next to each logic record the format, so verify against the client
  before changing one.
- Delayed item work (a dice landing, a door closing) is scheduled on `RoomTimerSystem`, keyed by
  the item, and cancelled in `OnPickupAsync`. Never `Task.Delay` inside a grain turn.
- Protocol state values the client interprets (`DiceStates`, `WheelStates`, `StickieColors`)
  live as static classes under `Turbo.Primitives/Furniture/`; durations and limits are
  `RoomConfig` tunables.
- Validate client data in the action module before the logic sees it: colour must be in the
  palette, text within `StickieTextMaxLength`, map entries within the `ObjectData*` limits.
  Reject with a `LogWarning` naming the item, room and player.

### Inventory sections
- Furniture, pets and bots are three modules of `InventoryGrain` with one shape
  (`Inventory{Furni,Pet,Bot}Module`): `EnsureReadyAsync` loads the section on first use,
  `GetAsync` / `GetAllAsync` read it, and every change writes the row before the list and then
  tells the presence. The grain partials only forward; put behaviour in the module. A section
  lists only what is in no room.
- Inventory items are built by `IInventoryFurnitureLoader` and nowhere else, so loaded, granted,
  picked-up and traded items read their extra data and stuff data the same way.
- Tell the presence once per change, not once per item: `OnFurnitureAddedAsync` and
  `OnFurnitureRemovedAsync` take the whole batch, and a grant of N items is one insert. The
  owner name comes from `InventoryGrain.GetOwnerNameAsync`, cached per activation.
- Ownership caps (`MaxPets`, `MaxBots`) count rows, placed or not, in the same context as the
  insert. A catalog purchase validates every product before it creates anything
  (`ValidateProduct` then `GrantProductAsync`).

### Badges
- A badge is a code a player owns (`player_badges`); it needs no definition to exist. Badges are
  the fourth section of `InventoryGrain` (`InventoryBadgeModule`), with the shape of the other
  three, and every grant, removal and "wear these" goes through it. `PlayerGrain` no longer
  knows about badges; do not add a second way to give one.
- Owner count and rarity are the hotel's, not the player's. They live in `BadgeDirectoryGrain`
  (one grain: owner counts per code, recounted on a timer and adjusted on each grant) and are
  filled into a `PlayerBadgeSnapshot` on every read. The inventory never stores them, because a
  copy would go stale the moment someone else got the badge.
- Because every badge shown anywhere asks the directory, it answers from memory only; its one
  query is the recount. Work that queries per request lives in `BadgeLeaderboardGrain`
  (`IBadgeLeaderboardGrain`), which asks the directory which codes are of a tier
  (`GetCodesOfRarityAsync`). The calls go one way: leaderboard → directory, never back.
- The client only draws the rarity it is sent (`BadgeRarityType` mirrors its `BadgeRarity`
  ids). How an owner count becomes a tier is therefore hotel data: the owner-share limits in
  `BadgeConfig`, with `MinimumPlayersForRarity` keeping a small hotel from calling everything
  unique. A `badge_definitions` row pins a rarity regardless of owners (staff badges).
- Every badge on the wire carries `ownerCount` and `badgeRarityId` (`Badges`, `BadgeReceived`,
  `BadgeInfo`, `HabboUserBadges`). The `Badges` list does not say what is worn: the client
  learns that from its own `HabboUserBadges`, so the two are sent together.
- A change to what is worn reaches the room through the presence
  (`OnSelectedBadgesChangedAsync` → `IRoomGrain.SetPlayerBadgesAsync`, told with `LogAndForget`),
  which updates the codes the wired "wearing badge" condition reads and broadcasts
  `HabboUserBadges`, since the client's room handler listens for it for every user.
- Leaderboards (`BadgeLeaderboardType`: total badges, one rarity tier, achievement level) are
  grouped queries over `player_badges` in `BadgeLeaderboardGrain`, cached per chunk for as long
  as the client treats a chunk as fresh. Players on the same score share a rank. The
  achievement board is empty until achievements exist. Type, tier, chunk index and size all
  come from the client and are bounded in the leaderboard grain.
- A player's badges rank (info stand, profile) is asked of the leaderboard grain with the number
  of badges, not the player: it keeps how many players hold each total (one grouped query per
  cache window, one row per distinct total) and answers from that, so ranking every avatar in
  every room costs no query. The client shows a rank of zero or more and hides a negative one;
  no badges, bots and "not known yet" are `BadgeRanks.NONE`.
- The rank is worked out where the badge count is, in the inventory
  (`InventoryBadgeModule.RefreshRankAsync`): when a badge is given or removed, and when the
  presence asks on room entry, because a rank also moves when other players get badges. The
  inventory *tells* the player grain (`IPlayerGrain.SetBadgesRankAsync`, `LogAndForget`), which
  keeps it for `PlayerSummarySnapshot.BadgesRank`; a changed rank goes presence → room as a
  `UserChange`. A first entry after activation therefore shows no rank for a moment and then
  the rank. Do not make `GetSummaryAsync` fetch it: that call is on every hot path.
- The player grain never awaits the inventory (inventory → presence → player grain is already a
  chain). So a profile's badge figures do not pass through it: the handler reads
  `GetExtendedProfileSnapshotAsync` and `IInventoryGrain.GetBadgeSummaryAsync` side by side
  (`ExtendedProfileExtensions.SendExtendedProfileAsync`) and the composer carries both.
- A client may claim a badge only through a request code the hotel lists
  (`BadgeConfig.RequestableBadges`); the badge code itself is never taken from the client.

### Pets and bots
- A pet or bot is a row (`pets`, `bots`) that is either in its owner's inventory
  (`room_id` null) or standing in a room. The hand-over is a grain call that moves the row
  first (`IInventoryGrain.TryCheckOut*Async` / `Return*Async`), so a crash in between leaves
  it where the database says it is. Rooms load theirs through `IRoomNpcProvider` and write
  stats back through `IRoomPersistenceGrain.EnqueueDirty*Async`; never update those rows from
  anywhere else.
- In a room they are avatars (`IRoomPet`, `IRoomBot`) with logic `default_pet` /
  `default_bot`; behaviour over time lives in `RoomPetTickSystem` / `RoomBotTickSystem`,
  actions in `RoomPetModule` / `RoomBotModule`. Pet commands arrive as chat
  ("&lt;name&gt; &lt;word&gt;") and are matched against `PetConfig.CommandWords`, which must agree
  with the hotel's `pet.command.<id>` texts.
- Pet-related furniture is a `[RoomObjectLogic]` like any other: `pet_food`, `pet_drink`,
  `pet_toy`, `pet_nest` (`IPetSupplyLogic`), `pet_breeding_nest`, `pet_package`, `pet_saddle`,
  `pet_revive`, `pet_fertilizer`, `pet_dye`, `pet_custom_part`, `monsterplant_seed`. The stock Habbo
  names are mapped to these by the data migration `MapPetFurnitureLogic`; extend that mapping
  rather than editing rows by hand. A product used on a pet is a
  `UseWithPetInteraction` on the item; what an item applies (package contents, a hair part) is
  a JSON section read with `FurnitureExtraDataSections.Read` from the item's extra data, with
  the definition's extra data as the per-type default.
- Wire shapes (pet figure struct, `PetInfo` field order, bot skill ids, chatter string) come
  from the Flash client and are recorded on the enums and helpers under
  `Turbo.Primitives/Pets` and `Turbo.Primitives/Bots`; verify against the client before changing.

### Trading
- Trades live in `RoomTradeGrain` (`IRoomTradeGrain`, keyed by room id), not in the room grain:
  adding items reads an inventory and the commit writes two, and none of that may hold up the
  room's turn. Handlers talk to the trade grain only. The room keeps the two things only it
  knows (`RoomTradeModule`): who the clicked avatar is and whether the room's trade mode lets
  each side trade (`GetTradePartiesAsync`), and the trading status on the avatars
  (`SetTradingStatusAsync`).
- The calls go one way. The trade grain awaits the room; the room never awaits the trade grain.
  When a trading player leaves, the room fires `CloseForPlayerAsync` with `LogAndForget` and
  carries on. Awaiting it would deadlock, because ending a trade calls the room back to clear
  the status.
- Trades are not persisted. Offers hold inventory snapshots and reserve nothing, so the commit
  re-checks and moves each side through `IInventoryGrain.TransferFurnitureAsync` (one
  owner-change statement per side, then the receiving inventory is told). A half-failed commit
  is handed back and closed with `TradeCloseReasonType.CommitError`. Deactivation closes
  whatever is still open.
- The flow mirrors the client's state machine: accept/unaccept while open, both accepted →
  `TradingConfirmation` (client countdown), both confirm → items move → `TradingCompleted`;
  a decline drops both acceptances. Any offer change also drops them.

### Wired
- Every wired box is a `[RoomObjectLogic("wf_...")]` under `Turbo.Rooms/Object/Logic/Furniture/Floor/Wired/`
  deriving from the base of its kind (`FurnitureWiredTriggerLogic`, `...ConditionLogic`,
  `...SelectorLogic`, `...AddonLogic`, `...ActionLogic`, `...VariableLogic`). The boxes on one
  tile form a stack that `RoomWiredSystem` runs: trigger → selectors (filter and invert are
  applied there) → addons mutate the `WiredPolicy` → conditions → the actions the outcome picks.
  Actions whose `IsNegative` is set (the `wf_act_neg_*` boxes) run when the conditions fail.
- Int params follow the Flash client editor exactly; declare them with `GetIntParamRules()` and
  read them with `GetIntParamOrDefault`. Two-int longs (`pushIntAsLong`) are read with
  `GetLongParam`, the "value or another variable" operand block with `TryResolveOperand`.
  Two-slot boxes (move furni to, furni to furni, send signal) read each slot with
  `WiredSlotSelection.ForSlot`; the second slot is `StuffIds2`.
- Conditions override `EvaluateCore` and state the positive rule only; the base applies the
  `wf_cnd_not_*` negation and the client invert switch.
- `IWiredContext.GetSelection(box)` is the only way a box resolves its inputs. It is synchronous
  because it only reads room state; never reintroduce an async twin or block on a task
  (`GetAwaiter().GetResult()`) inside a grain. When a synchronous caller needs a module answer,
  give the module a synchronous core (`CanPlaceFloorItem`) and let the async method wrap it.
- Wired time is counted in half-second pulses. Convert through `WiredPulses`; do not write the
  500 again.
- Anything that happens over time is an event the system consumes: signals
  (`WiredSignalEvent`, antennas are picked furni), stack calls (`WiredStackCalledEvent`,
  bounded by `WiredConfig.MaxDepth`), clocks (`WiredClockTickEvent` from
  `FurnitureCounterClockLogic`), games and scores (`RoomGameSystem` events), variable writes
  (`WiredVariableChangedEvent`), avatar actions (`PlayerPerformsActionEvent`) and item use
  (`RoomItemUsedEvent`). Periodic and "at given time" triggers are paced by
  `RoomWiredSystem.Timers` from the tick, not by an event.
- Movement of furni and users goes through `IWiredExecutionContext` (`ProcessFloorItemMovementAsync`,
  `ProcessUserMovementAsync`, `ProcessUserDirectionAsync`); the system flushes one
  `WiredMovements` packet per action. Text an action shows goes through `FormatTextAsync` so the
  placeholder addons apply. Selections carry player ids and furni object ids; bots are not in
  them, bot actions resolve the named bot from their string param.
- Variable boxes key user values by player id; the wired menu addresses users by room index and
  `RoomWiredSystem.ResolveTargetId` maps between the two. Derived variables (level-up, time
  utility) implement `IWiredSubVariableProvider` on the variable box tile and are rebuilt with
  the variable boxes. Value timestamps live in `KeyValueStore.Timestamps`
  (`Turbo.Rooms/Wired/Storage/`).
- Freeze effect ids are `WiredConfig.FreezeEffectIds`; the list ships as zeros and is hotel
  data. Team effect ids belong to the game, not to wired: `RoomConfig.GameTeamEffectIds`.
- Everything in a wired save comes from a client and is checked before it is stored, in
  `FurnitureWiredLogic.ApplyWiredUpdateAsync`; the same checks run again when a box loads, and
  stored ints that fail them are replaced by the box defaults. A new box gets this for free as
  long as it declares its inputs honestly:
  - One rule per int param, the narrowest that fits: `WiredEnumParamRule<T>`,
    `WiredBoolParamRule`, `WiredRangeParamRule`, or a shared one from `WiredRules`
    (`VariableTarget`, `HandItem`, `Effect`, `TileOffset`, `NonNegative`). `WiredRules.AnyInt`
    is only for values with no bound at all (half of a 64-bit value, a bit mask, a variable's
    value). Never cast an unchecked int to an enum, and never use a client int as a loop bound
    or an array index; rectangles go through `WiredArea`, which cuts them to the map and to
    `WiredConfig.SelectorMaxAreaSize`.
  - Text is cut to `GetStringParamMaxLength()` and stripped of control characters on save.
    Override it with the client's own input limit for the box (`TextInputParam`/`TextAreaParam`
    in the AS3 editor class) instead of truncating at execution time. Text that is expanded
    (`FormatTextAsync`) is capped again after expansion. A regex built from player text is
    escaped and run with a match timeout.
  - Picked furni must be in the room, are de-duplicated and capped by
    `WiredConfig.SelectedItemsLimit`; variable ids must parse (`WiredVariableId.TryParse`), exist, and
    fit `GetMaxVariableIds()`; sources outside `GetAllowed*Sources()` fall back to the default.
  - Malformed input is refused or dropped without throwing. Do not catch an index or parse
    exception to detect it: a client could fill the log one request at a time.
- A box that creates value outside the room (`wf_act_give_reward`: badges, furniture) raises
  `MinimumControllerLevelToSave`, so holding the room's wired permission is not enough to
  configure it. The wired menu writes a variable only when the variable allows writes and the
  target furni or user is in the room right now.

### System boundaries: wired is a user of the room, not a part of it
Wired touches everything, so it is where systems bleed into each other first. These rules came
out of pulling it apart; they hold for any system that grows the same way.
- **Wired lives in its own tree.** `Turbo.Rooms/Wired/**` (runtime, rules, variable storage),
  `Turbo.Rooms/Object/Logic/Furniture/Floor/Wired/**` (every `wf_*` furni, the counters
  included), `RoomWiredSystem*.cs`, `RoomGrain.Wired.cs` / `IRoomGrain.Wired.cs`, and the
  `.../Wired/` folders in `Turbo.Primitives`. A wired-only type anywhere else is misplaced,
  however neutral its name: the variable stores sat in `Grains/Storage`, two wired events in
  the events root, the wired grain methods in the floor-furniture partials.
- **Wired settings are `WiredConfig`** (`Turbo:Wired`), and only what wired code reads goes in
  it. A `Wired` prefix is not a reason: the bot follow distance (`BotConfig.FollowDistance`),
  the game duration and the team effect ids (`RoomConfig.Game*`) were all named `Wired*` while
  wired never read them.
- **A general module does not recognise a wired box.** No `is IWiredBox`, no
  `is FurnitureWiredLogic`, no wired error code outside the wired tree. When general code needs
  wired to have a say, it offers a seam that does not name wired and wired plugs into it in
  `RoomGrain`'s constructor, the one place that knows every system:
  - refusing a placement: `IRoomPlacementLimit`, asked through
    `RoomFurniModule.EnsureWithinPlacementLimits` (the wired box cap);
  - reacting to a change: a room event. `RoomSecurityModule` publishes
    `PlayerControllerLevelChangedEvent`; the wired system answers it with the wired permissions.
    The security module and the presence grain used to compute and carry `canModifyWired` /
    `canReadWired` themselves.
- **What wired drives is not wired's.** Teams, scores and the running game are
  `RoomGameSystem` (`GameTeamType`, `GameStartedEvent`, `GameEndedEvent`,
  `GameScoreChangedEvent`): a counter furni starts a game, wired boxes join teams and give
  points, wired triggers listen, and banzai or freeze furni will use the same system. Frozen is
  avatar state (`IRoomAvatar.SetFrozen(isFrozen, thawsOnTeleport)`), not a set kept by whoever
  froze it; that set was never cleared when the avatar left.
- **A wired action asks the owning module; it does not redo the module's work.** A box picks
  the targets and the parameters, then makes one call: `RoomModerationModule.KickPlayerBySystemAsync`
  / `MutePlayerBySystemAsync`, `RoomChatSystem.WhisperToPlayerAsync`,
  `RoomFurniModule.MoveFloorItemAsync`. When the module has no such method, add it to the module
  (`...BySystemAsync` for "no actor, no rank check") and call that. The copies had already
  diverged: the wired kick protected only the owner while the wired mute protected staff too,
  and wired moved furni straight on the map, so `OnMoveAsync` never ran and a roller or wired
  box moved by wired kept its old tile in the roller index and the wired stacks.
- **One system may batch what another announces, not skip what it does.** Wired sends the moves
  of an action as one `WiredMovements` packet, so it passes `announce: false`; the map update and
  the logic callback still go through the furni module.
- **Wired reads the room through the modules, not through `_state`.** The avatar and furni
  modules have a read surface for everyone who is not them: `RoomAvatarModule.Avatars` /
  `Players` / `TryGetAvatar` / `GetAvatarsOnTile` / `HasAvatarOnTile` / `GetAvatarsOnItem` /
  `TryGetNearestPlayer`, `RoomFurniModule.Items` / `TryGetItem` / `TryGetFloorItem` /
  `GetFloorItemsOnTile` / `IsHighestOnTile`, `RoomMapModule.IsTileDisabled`. They are
  bounds-safe, so a caller does not repeat the `InBounds` check or index a tile array. The only
  `_state` members wired code still touches are `RoomSnapshot` (its own masks and timezone) and
  `NextWiredBoundaryMs` (set with the other tick boundaries). Before reading `_state` from
  outside the owning module, look for the accessor; when it is missing, add it to the module.
- **Putting an avatar somewhere is `RoomAvatarModule.RelocateAvatarAsync`.** It stops the walk,
  tells the furni left and the furni landed on, moves the avatar on the map and fixes its
  height. Wired teleports and carried avatars use it; the walked step in `RoomAvatarTickSystem`
  shares its `NotifyWalkOffAsync` / `NotifyWalkOnAsync`. Whether the avatar may stand there, and
  how the room is told (wired batches a `WiredMovements` packet), stay with the caller.
- **Which tile is "toward" or "away from" something is the map's** (`RoomMapModule.GetStepsToward`
  / `GetStepsAwayFrom`, straight steps, longer axis first). The chase and flee boxes used to do
  it with index arithmetic of their own, each differently. A box decides what the furni wants;
  the map says where that is; the furni module says whether it fits.
- **A bubble nobody typed is `RoomChatSystem.SayAsAvatarAsync`** with an `AvatarSpeech` (style,
  shout, whisper to one player, width): bot lines and orders, pet replies, the wired message
  box, the word a player gets before a wired kick or mute. Six places built those composers by
  hand, and only some of them filtered the text; the chat system filters all of it. It is
  deliberately not chat: no flood check, no commands, no chat log, no `PlayerChatEvent`.
- **A general event speaks the room's language, the listener translates.**
  `PlayerPerformsActionEvent` carries an `AvatarActionType` and the expression, dance, sign or
  posture it was; `WiredAvatarActionMatcher.TryTranslate` turns that into the wired editor's
  own numbering. The avatar module used to publish `WiredAvatarActionType` and map expressions
  onto it, with anything unknown counted as a wave.

### Grains are not reentrant: side effects on other grains that call back go in the service
- `RoomGrain` and `PlayerPresenceGrain` call each other. A room grain that awaits a presence grain
  which in turn calls the room (for example `ClearActiveRoomAsync` → `RemoveAvatarFromPlayerAsync`)
  deadlocks, because grains are single-threaded and non-reentrant.
- So a room operation that must close a player's session (kick, ban, delete) is split: the grain
  validates and removes the avatar and returns a result; `RoomService` then calls the presence
  grain. Entry, doorbell and close already work this way; follow them.
- Closing the session of a player the room has already removed is
  `IPlayerPresenceGrain.OnRemovedFromRoomAsync(roomId, kicked)`. It never calls the room back, so
  it is the one eviction call that is safe from inside the room grain; `RoomService` uses the same
  call for kicks, bans and room deletion. Code that runs in the room tick (a wired kick) does not
  await it: it goes out with `LogAndForget`, because the presence may itself be waiting on the room.
- The inventory grain awaits the presence grain whenever a section changes, so the presence must
  never await the inventory. The badge calls on the presence only send what they are given, and
  the handler fetches the list first (`GetBadgesMessageHandler`). The older
  `Open{Furniture,Pet,Bot}InventoryAsync` calls do ask the inventory from inside the presence;
  they predate this rule and can deadlock against a concurrent add. Do not copy them, and move
  them to the handler-fetches shape when they are next touched.
- The same one-way rule holds for every helper grain of a room: `RoomTradeGrain` awaits the room,
  so the room only ever tells it things with `LogAndForget`. Before adding an awaited call from
  grain A to grain B, check that nothing B awaits leads back to A.
- Never mark a grain `[Reentrant]` to make such a chain compile. It moves the bug from a deadlock to
  interleaved state.

## Profile and grain flow constraints
- Keep packet handlers orchestration-only:
  - validate input
  - call grains through canonical grain-factory access patterns
  - map snapshot data to outgoing composers
- Do not query database contexts or repositories directly from packet handlers.
- Keep persistence access in grains/services/providers that own domain state.
- Do not use ad-hoc grain key strings in handlers when extension-based access exists.
- Do not add silent `catch` blocks in handlers.
- When snapshot fields are available, map them into composer payloads instead of TODO placeholders.
- For incoming message records in `Turbo.Primitives/Messages/Incoming/**`, keep mandatory fields explicit (use `required` where appropriate) instead of default-fallback contracts.

## Session and room routing constraints
- Connection/session lifecycle starts in gateway flow; do not duplicate session registration in handlers.
- Post-SSO session attachment goes through `PlayerPresenceGrain` (one active presence grain per player id).
- There are exactly three ways to send a composer; pick by who it is for:
  - **The session that sent the packet being handled**: `ctx.SendComposerAsync(composer, ct)` in
    the handler. It is a reply to that connection and needs no grain hop.
  - **A player** (someone else, or anything a grain, module, logic class or service pushes):
    `grainFactory.SendComposerToPlayerAsync(playerId, composer, ct)`, or
    `SendComposerToPlayersAsync` for several. These are the `GrainFactoryExtensions` wrappers
    over the presence grain, which fans out to the player's sessions. Do not spell out
    `GetPlayerPresenceGrain(id).SendComposerAsync(...)` and do not add a local
    `SendToPlayerAsync` helper; five of those existed before they were folded into the
    extension. The list overload (`presence.SendComposerAsync(composers, ct)`) stays a direct
    presence call, because it is a batch for one player.
  - **Everyone in a room**: `RoomGrain.SendComposerToRoomAsync` (the room stream).
- Do not send directly to raw sockets/session transports from packet handlers.
- Active-room membership/discovery belongs to `RoomDirectoryGrain`; do not bypass it with ad-hoc room tracking.
- Grain lifetime remains Orleans-managed by default; use `[KeepAlive]` only for explicitly justified directory/manager grains.

## Packet addition checklist (revision work)
When adding packet mappings in `Turbo.Revisions/Revision20260909`:
1. Update `Turbo.Revisions/Revision20260909/Headers.cs`:
   - add/update incoming `MessageEvent` id constants
   - add/update outgoing `MessageComposer` id constants
2. Add parser class under:
   - `Turbo.Revisions/Revision20260909/Parsers/<Domain>/*MessageParser.cs`
3. Add serializer class under:
   - `Turbo.Revisions/Revision20260909/Serializers/<Domain>/*MessageComposerSerializer.cs`
4. Register mappings in:
   - `Turbo.Revisions/Revision20260909/Revision20260909.cs`
   - incoming: `Parsers` dictionary with `MessageEvent` key
   - outgoing: `Serializers` dictionary with composer type + `MessageComposer` id
5. Ensure the required `using` directives are present in `Revision20260909.cs` for new parser/serializer namespaces.

## Task recipes

### Add packet handler
- Required context files:
  - `AGENTS.md`
  - `CONTEXT.md`
  - `Turbo.Primitives/Orleans/GrainFactoryExtensions.cs`
- Required references:
  - one handler in same domain under `Turbo.PacketHandlers/<Domain>/`
  - related incoming message type under `Turbo.Primitives/Messages/Incoming/**`
- Forbidden changes:
  - no direct DB access in handler
  - no direct session/socket sends
  - no ad-hoc grain key literals when extension methods exist
- Validation:
  - `dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudFastCheck`
  - `dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudQualityGate`

### Change grain behavior
- Required context files:
  - `AGENTS.md`
  - `CONTEXT.md`
  - target grain interface in `Turbo.Primitives/**/Grains/*.cs`
- Required references:
  - existing grain in same module
  - related snapshot/state types in `Turbo.Primitives/Orleans/Snapshots/**` or `States/**`
- Forbidden changes:
  - no handler-layer fallback logic that bypasses grain ownership
  - no lifecycle changes that abuse `[KeepAlive]` without infrastructure justification
- Validation:
  - `dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudFastCheck`
  - `dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudQualityGate`

### Add message/composer mapping
- Required context files:
  - `AGENTS.md`
  - `CONTEXT.md`
  - neighboring message/composer classes
- Required references:
  - incoming message under `Turbo.Primitives/Messages/Incoming/**`
  - outgoing composer under `Turbo.Primitives/Messages/Outgoing/**`
  - handler using same message family
- Forbidden changes:
  - no placeholder payload collections when source snapshot data exists
  - no implicit default-fallback contracts for mandatory incoming fields
- Validation:
  - `dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudFastCheck`
  - `dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudQualityGate`

### Refactor lookup/cache logic
- Required context files:
  - `AGENTS.md`
  - `CONTEXT.md`
  - current lookup owner grain/service
- Required references:
  - existing set/invalidate methods
  - all reverse-lookup call sites
- Forbidden changes:
  - no one-way cache updates; forward/reverse mappings must stay coherent
  - no loss of case-insensitive semantics for username lookups
- Validation:
  - `dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudFastCheck`
  - `dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudQualityGate`

## Keeping the codebase from drifting
Most drift here came from adding the second copy of something that already existed. Before
finishing a change, check it against this list; each line is a mistake that was made and fixed.
- **Search before adding.** A config key, constant, helper, event or grain method: grep for an
  existing one first. `WiredMaxDepth` (now `WiredConfig.MaxDepth`) existed unused while a second depth setting was added beside
  it. An unused setting is a signal to wire it up or delete it, not to add a sibling.
- **One way to do a thing.** If a change makes two members do the same job (two selection
  methods, two eviction paths, two ways to build an inventory item), collapse them in the same
  change and move every caller. Aliases kept "for compatibility" inside this repository are drift.
- **Sibling features share a shape.** Inventory sections, wired box kinds, pet and bot modules:
  when one gets a capability (batching, a `GetAsync`, a delete), give it to its siblings or say
  why not. Behaviour lives in the module; grain partials forward.
- **Batch across grain boundaries.** A loop that makes a grain call per item is a bug in waiting.
  Pass the whole set (`OnFurnitureAddedAsync`, `AddFurnitureFromRoomItemSnapshotsAsync`), group
  by target grain, and run independent targets with `Task.WhenAll`. Returning furni, pets and bots
  on room deletion all follow that shape: the room lets go first, then each owner's inventory
  takes its share back concurrently.
- **One public type per file, in the folder for its kind** (see Type placement). A base class and
  its two registered subclasses are three files.
- **No silent catch, no blocking wait, no discarded task.** All three hide failures that only
  show up under load.
  - A packet handler does not wrap its body in `try { } catch (Exception) { }`: `PackageHandler`
    already logs every handler failure with the packet header and session. A handler catches only
    a typed exception it turns into a reply (`CatalogPurchaseException` → `NotEnoughBalance`).
  - `_ = SomethingAsync(...)` is `.Ignore()` by another name. Await the task, return it, or end it
    with `.LogAndForget(logger, "what it was doing")`. Handlers await their sends. Room code that
    must not hold up the tick (broadcasts, event publishes, wired box flashes) uses `LogAndForget`.
  - Do not use an exception as a lookup (`First` inside `try`, an index inside `try`): use
    `TryGetValue`, a bounds check or `GetIntParamOrDefault`. The only commented swallows left are
    in runtime primitives that have no logger and run during unload (`ReloadableExport`,
    `CompositeDisposable`); do not add new ones.
- **Shared room lookups have one home.** Player id → avatar is `RoomAvatarModule.TryGetPlayer`;
  do not walk `AvatarsByPlayerId` then `AvatarsByObjectId` inline, and do not park a general
  helper in whichever module needed it first (it lived in the pet module, and trading reached
  into pets to find a player). When a second module needs a helper, move it to the module that
  owns the state before calling it.
- **Two classes that differ in one line share a base.** Before copying a class to change a
  detail, extract what stays the same and leave the detail abstract: the wired neighbourhood
  selectors (`FurnitureWiredNeighborhoodSelectorLogic`), the furni placement variables
  (`FurniturePlacementVariable`), the pet products (`FurniturePetProductLogic`) and the six wired
  save handlers (`UpdateWiredMessageHandler<TMessage>`) were each written out in full per
  variant, and the copies had already drifted (only the trigger save reported a refused save).
  When the variants cannot share a base (a wired action and a wired condition), put the shared
  lifecycle in the common ancestor behind an opt-in flag (`KeepsFurniSnapshot`).
- **Declarations repeat too.** A list or block that many boxes declare identically gets a name:
  `WiredSources.Users` / `Furni` / `PickedFurni`, `AllVariablesContext()`, `GetTargetType(...)`.
  A box spells its own list out only when it really differs.
- **A composer that mirrors a snapshot carries the snapshot.** Do not copy twenty fields from a
  snapshot into a composer in every handler (`ExtendedProfileMessageComposer { Profile = ... }`);
  the serializer reads the snapshot.
- **A second private method with the same body is a copy.** The wired mute was added as a
  duplicate of the player mute; both now go through `StoreMuteAsync`. Same for two classes that
  are identical but for the name (`PackageEncoderWs` was `PackageEncoder`).
- Scan for copies with a script over `git ls-files -co --exclude-standard`, not plain
  `git ls-files`: files that are new in the working tree are the most likely to hold them, and
  a tracked-only listing skips exactly those.
- **A bulk replace must skip the definition it points at.** When call sites are rewritten to use
  a new helper by script or search-and-replace, the helper's own body matches the pattern too.
  `RoomAvatarModule.TryGetPlayer` was rewritten to call itself this way and overflowed the stack
  the first time a player entered a room; it compiled, and no gate catches it. Exclude the
  defining file or method from the replace, then read the helper again afterwards.
- **A diff is computed, not assumed.** When the client sends what it already holds (ids with
  hashes), compare before resending; the wired variable sync resent every variable on each
  request because the hash was read and never used.
- **A claim about the client is checked in the client.** Behaviour attributed to the Flash client
  (what it sends after `CloseConnection`, what a param means) is read from the AS3 source before
  it is written into code or a comment. Where the source only has localisation keys, the
  assumption is recorded on the enum.
- **A feature that moves takes its leftovers with it.** When badges left `PlayerGrain`, the
  usings they needed stayed behind in `PlayerGrain` and `PlayerLiveState`; no gate flags an
  unused using, so read the file you removed code from once more.
- **Build output is part of the diff.** The gates fail on errors, not on warnings, so read the
  warnings for the files you touched. `GetBadgeInfoMessageHandler` awaited its two tasks again
  after `Task.WhenAll` without `ConfigureAwait(false)` and added two CA2007 warnings; read
  results into locals with `ConfigureAwait(false)` like every other handler await.
- **A new setting looks like its siblings.** `BadgeInventoryFragmentSize` landed beside three
  `required`, documented fragment sizes with appsettings keys, and had none of the three. A
  config option is `required`, has a summary, and has its key in `appsettings.json`.
- **The adapter files say what this file says.** `CONTEXT.md` and
  `.github/copilot-instructions.md` still sent revision work to the plugin repo after it moved
  to `Turbo.Revisions/`. When a rule here changes, grep the adapters (`CONTEXT.md`, `CLAUDE.md`,
  `CODEX.md`, `.github/copilot-instructions.md`) for the old wording in the same change.
- **A rule that lost is deleted, not left beside the winner.** This file told grains to take
  limits from handlers in one section and from their module config in another; new code picked
  either. When two rules disagree, find which one the code follows, delete the other here and in
  the adapters, and remove what the dead rule left in the code (the unused `IConfiguration`
  injections).
- **A placeholder on the wire is checked in the client.** `BadgesRank = 0` looked harmless and
  was sent for every avatar; the client draws any rank of zero or more, so everyone was "#0".
  Before leaving a `0` or `// TODO` in a snapshot a serializer writes, read what the client does
  with that value and send its "nothing" (`BadgeRanks.NONE`) instead.
- **Ask whose it is before where it fits.** New state, a new setting or a new method goes to the
  system that owns the concept, not to the one that needed it first (see **System boundaries**).
  A `Wired` prefix on something in a general class, or a general module checking for a wired
  type, is the sign it landed in the wrong place.
- **Remove what a change orphans**: the setting nothing reads, the interface member with no
  caller, the using, the appsettings key. A setting added "for later" (`OnlineTimeMinutes`,
  `MinutesBetweenMountAttempts`) is an orphan from the day it lands: add it with the code that
  reads it.
- When a fix teaches a rule that is not in this file yet, add it here in the same change.

## Required validation before completion
```bash
dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudFastCheck
dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudQualityGate
```

## Definition of done for AI changes
- All modified files match nearby patterns and contract rules.
- Quality gates pass with no new warnings introduced by the change.
- Architecture invariants for touched areas are explicitly confirmed in PR.
- Edge/failure behavior is addressed for logic changes.
- Any context-rule updates needed by the change are included in the same PR.

## PR expectations for AI-assisted work
- Disclose AI usage and major generated sections.
- Be able to explain complex generated logic in your own words.
- Include verification of at least one edge/failure scenario when behavior changes.
