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
  The same applies to a factory that builds a derived snapshot from an instance of its base, not
  just to an entity mapping. **Most derived snapshots in the tree do not do this yet** — of the
  derived records under `Turbo.Primitives`, only `RoomSnapshot`, `ClubExtendOfferSnapshot` and
  `GuildSnapshot` take their base in a constructor; the rest either add no fields (nothing to
  do) or re-list the base's fields by hand, and those are the ones to convert as you touch them.
  This is not a tidiness rule. `RoomActiveSnapshot.From` copies nineteen base fields one by one
  and silently stopped copying `HiddenByBc` when that field moved up to `RoomInfoSnapshot`; the
  field is not `required`, so nothing failed to compile, and a room hidden by Builders Club goes
  on being listed by the navigator for as long as it is active, because `NavigatorService`
  filters live rooms on exactly that field. A `: base(room)` constructor could not have lost it.
- What a furni leads to is stored on the item, in the `RoomLinkerData` extra data section: a
  fixed `RoomId` for a room linker, the paired `ItemId` for a teleporter. There is no link table.
  A pair's room is never stored, because either half can be picked up and placed elsewhere;
  resolve it when needed (`RoomFurniModule.GetRoomIdOfItemAsync`). A dangling `ItemId` (the pair
  was deleted) simply leads nowhere.

### Constants and magic values
- Do not scatter hardcoded literals or `const` fields through implementation files. Give them a home:
  - a value the protocol or domain defines (message types, result codes, entry kinds) becomes an
    **enum** in `Turbo.Primitives/<Domain>/Enums/`, and the serializer casts it. The composer or
    snapshot field has the enum's type; an `int` field with `(int)SomeType.Member` at the call
    site is the same cast in the wrong place (`RoomSettingsErrorEventMessageComposer.ErrorCode`,
    `VariableFxConfigSnapshot.ShowMode` / `Category`, `PurchaseErrorMessageComposer.ErrorCode`,
    which sat one `switch` away from `PurchaseNotAllowedMessageComposer.ErrorType` carrying the
    same enum properly). Zero counts: when the client reads `0` as
    an answer ("can rent"), the enum gets that member (`RentableSpaceRentFailedType.None`)
    instead of a `const Type X = 0` beside the code that sends it;
  - a set of related identifiers the client sends (search codes, cache keys) becomes one **shared
    static class** in `Turbo.Primitives/<Domain>/` (see `NavigatorSearchCodes`,
    `NavigatorListingKeys`);
  - anything an operator may want to tune (limits, lengths, timeouts, intervals, caps) becomes a
    **config option** on the module's config class, bound from `appsettings.json`.
- A unit conversion is not a constant to declare: `MS_PER_SECOND = 1000` appeared in two furni
  classes in one change. Convert through `TimeSpan` (`TimeSpan.FromSeconds(x).TotalMilliseconds`),
  or through the domain's own helper where one exists (`WiredPulses`).
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
  pattern-matches it in `OnInteractAsync` and checks its own permissions with `HasRightsAsync`,
  `IsItemOwner` (the item's owner only — for turning an item into value: exchange, present, pet
  package, seed) or `IsItemOrRoomOwnerAsync` (for changing how it stands: mannequin, trophy),
  refusing through `Reject(...)` so the refusal is logged with ids. A plain
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
- A table of protocol state values the client interprets lives as a static class under
  `Turbo.Primitives/Furniture/` (`DiceStates`, `WheelStates`, `DimmerStates`, `StickieColors`,
  `RentableSpaceStates`, `GateStates`), never as `private const int STATE_*` in the logic class.
  What makes it a table rather than a local constant is that a second reader exists or could:
  `GateStates` was two private copies of the same `CLOSED = 0 / OPEN = 1` in the gate and the
  one-way gate, which is how a table announces itself. A single logic's own field offsets and
  ranges (`STATE_INDEX`, `MIN_CHANNEL`, a wired box's `PARAM_*`) stay beside the class that
  reads them, because there is nothing to share and the doc comment above them records the
  format. Durations and limits are `RoomConfig` tunables either way.
- Validate client data in the action module before the logic sees it: colour must be in the
  palette, text within `StickieTextMaxLength`, map entries within the `ObjectData*` limits.
  Reject with a `LogWarning` naming the item, room and player.

### Furni that gives, rents or plays something
- `clothing_change` (a booth that dresses whoever walks in), `rentable_space` and `youtube` (a
  video display) are ordinary `[RoomObjectLogic]` classes whose client packets arrive as
  `FurnitureInteraction` records, like the dice and the dimmer.
- A room never awaits a change to a player's figure. `RoomAvatarModule.ChangePlayerFigure`
  tells the player grain with `LogAndForget`: the player grain tells the presence, the presence
  comes back to this room to update the avatar, and a room waiting on that waits on itself.
  The mannequin awaited it and would have hung for the grain timeout on every use.
- A furni that lets a player build without room rights implements `IRoomBuildArea`; the
  placement path asks through `RoomFurniModule.HasBuildAreaRights` once room rights said no,
  for a new item and for moving one's own item inside the area. It does not check for a rentable
  space. A rent lives in the item's extra data (`RentableSpaceData`), ends on a
  `RoomTimerSystem` timer that is set again in `OnAttachAsync`, and ending it sends what the
  renter built there home. The status message reuses the refusal enum, and the client reads
  `RentableSpaceRentFailedType.None` there as "can rent".
- Many items leave a room through `RoomActionModule.ReturnItemsToOwnersAsync`: the room lets go
  of all of them, tells the client once per owner (`ObjectRemoveMultiple`) and hands each
  owner's inventory its share in one call. Room deletion and an ended rent both use it.
- A player may always pick up their own furni, whatever their rights
  (`RoomActionModule.RemoveItemByIdAsync`); otherwise a renter, or a guest whose rights were
  taken, could never get it back.
- A video display keeps a clock, not a stream: which video, how far in, paused or not. The
  client plays it and never says when a video ends, so each video's length is config
  (`RoomConfig.YoutubePlaylists`) and the room moves on by timer.
- Which client packets the server still lacks is measured, not guessed:
  `python scripts/packetgap.py Room` (nitro-next is found through `NITRO_NEXT`) compares the
  composers nitro-next constructs and the messages it listens to with the handlers that do
  something and the composers something sends. Its "stub" and "unsent" are heuristics, so read
  the file before acting on a line. Still missing after this pass, each for want of
  a system rather than a handler: purchasable clothing (no clothing inventory), the guild furni
  menu (no guilds), the room queue and spectators, `ConfigurationItemStates`, `UseObject`,
  `SpecialRoomEffect` and `BotSkillListUpdate` (nothing on the server causes them).

### Temporary furni
- A temporary furni is a floor item the room makes from a definition alone
  (`RoomFurniModule.PlaceTemporaryFloorItemAsync`): on the map, in the furni list players are
  sent, open to wired, and nowhere else. It has no row, no inventory and no persistence, and it
  is gone when the room unloads. `wf_act_place_furni` makes them from its saved snapshot of
  the picked furni (`WiredFurniSnapshotEntry.DefinitionId`), `wf_act_remove_furni` takes them
  away and touches nothing else.
- Its id is negative, handed out by the room counting down (`RoomLiveState.NextTemporaryItemId`),
  and `IRoomItem.IsTemporary` is just that sign. A negative id cannot collide with a row id,
  the client shows no infostand for one, and every packet handler already refuses ids that are
  not positive, so a player cannot pick one up, move it or trade it.
- Whatever writes items somewhere asks `IsTemporary` first: the dirty-item hook is not set
  (`RoomObjectModule.AttatchObjectAsync`), removal queues no delete, a pickup is refused and
  `ReturnItemsToOwnersAsync` lets them go without handing them to an inventory. A new path
  that persists or returns room items needs the same check.
- They cost nobody anything, so the room caps them (`RoomConfig.TemporaryFurniMax`); a stack on
  a repeater would otherwise fill the room. Reaching the cap or a blocked tile is not an error.
- The ids start again at -1 each time the room loads, so anything keyed by furni id that
  outlives the room must let go when one leaves: the wired system drops the stored variable
  values of a temporary furni on detach (`ForgetStoredValuesOfTemporaryFurni`), or the next
  furni to get that id would inherit them.
- A new item has no logic until it is attached, and a check that type-tests `Logic` before
  then silently answers "not mine": the wired and variable-fx placement caps tested it and so
  refused nothing for as long as they existed. Placement calls `RoomObjectModule.EnsureLogic`
  before asking the limits; anything that runs earlier still falls back to the definition
  (`tItem.Logic?.CanWalk() ?? tItem.Definition.CanWalk`).
- **Placement is checked in the entry point, never at its callers.**
  `RoomFurniModule.PlaceFloorItemAsync` / `PlaceWallItemAsync` own the spot (`CanPlaceFloorItem`,
  one occupancy rule for new and moved items) and the limits. The limits used to be asked by two
  of four placement paths, so a Builders Club wired box skipped the cap.
- **A bounds question returns false; it does not throw.** `RoomMapModule.GetTileIdForSize` /
  `TryGetTileIds` answer by coordinate. It used to throw while its callers checked for `false`,
  and the wired movers guarded it three ways (a pre-check, a `try`, nothing).
- **"None" and a real id never share a value.** A tile with no top item said `-1`, and temporary
  furni ids count down from `-1`, so every empty tile named the first temporary furni. Use
  `RoomMapModule.NO_ITEM` (0) or a nullable.
- **Footprint maths is `FloorFootprint`** (`Turbo.Primitives/Rooms`): rotation swap, tiles,
  distance, adjacency. Two copies of the adjacency test forgot the east/west swap.

### Inventory sections
- Furniture, pets and bots are three modules of `InventoryGrain` with one shape
  (`Inventory{Furni,Pet,Bot}Module`): `EnsureReadyAsync` loads the section on first use,
  `GetAsync` / `GetAllAsync` read it, and every change writes the row before the list and then
  tells the presence. The grain partials only forward; put behaviour in the module. A section
  lists only what is in no room.
- **Pets and bots are one flow.** `InventoryUnitModule<TEntity, TSnapshot>` loads, hands over to
  a room, takes back, creates (under the owned cap) and deletes both; `InventoryPetModule` and
  `InventoryBotModule` say only what differs — the table, the snapshot, the cap, the columns a
  unit brings back from a room, the presence calls, and their catalog product. The entities
  share `IInventoryUnitEntity`, the snapshots `IInventoryUnitSnapshot`, the live state
  `InventoryUnitSection<T>`. The two modules were two hundred lines copied; a third kind of
  room-placed unit is a subclass, not a copy. (EF translates the interface-typed `Where`,
  `ExecuteUpdate` and `ExecuteDelete` in the base to plain column SQL; checked against MySQL.)
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
  read them with `GetIntParamOrDefault`, as the type the rule declares: a
  `WiredEnumParamRule<T>` param is read as `T` (fallback `T.Member`), a bool rule as `bool`.
  Reading an enum param as `int` and casting throws inside the accessor, and the fallback
  hides it: `GetTargetType` did that, so every box with a variable target ignored the target
  it was saved with and logged an exception on each run. A mismatch is now logged as an error
  naming the box class; treat that line as a bug in the box, not as bad data. A variable
  target is read through `GetTargetType(variable, index)`, not by hand.
  All three cases found were in shared helpers that take the index as an argument, where the
  rule is out of sight: `GetTargetType`, `TryResolveOperand` (its value-or-variable switch
  always read as "value", so an operand variable never worked) and `RequiresAll` (the "all
  must match" switch always read as off). A helper that reads a param says in its summary
  which rule the box must declare there, and a new one is checked against every caller's
  `GetIntParamRules()`. Two-int longs (`pushIntAsLong`) are read with
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
  (`WiredVariableChangedEvent`), avatar actions (`AvatarPerformsActionEvent`) and item use
  (`RoomItemUsedEvent`). Periodic and "at given time" triggers are paced by
  `RoomWiredSystem.Timers` from the tick, not by an event.
- Movement of furni and users goes through `IWiredExecutionContext` (`ProcessFloorItemMovementAsync`,
  `ProcessUserMovementAsync`, `ProcessUserDirectionAsync`); the system flushes one
  `WiredMovements` packet per action. Text an action shows goes through `FormatTextAsync` so the
  placeholder addons apply. Selections carry player ids and furni object ids; bots are not in
  them, bot actions resolve the named bot from their string param.
- **An internal variable's place in the editor's list is its id.** The client sorts by variable
  id, and `WiredVariableIdBuilder.CreateInternalOrdered` builds one from the target's band, the
  box's `SubBandType` and its `Order`, highest first. So the order is declared, never
  incidental: `python scripts/wiredvars.py` prints the list as the editor shows it. Leave gaps
  in `Order` for what is not written yet, because a variable's id is what a saved box stores
  and renumbering one loses every reference to it. The furni and user lists are numbered to a
  layout given in full, and the slots of the variables below are reserved and deliberately
  empty.
- **A variable with nothing behind it is not declared.** These have no system yet and are
  reserved rather than written: user `@level`, `@is_group_admin`,
  `@favorite_group_id`, `@team.type` and the six `@transaction.*`, because chests and
  contracts do not exist.
- **What a variable reads is on the avatar before it is asked for.** A variable is
  synchronous and must never await a grain, so anything an account owns is put on the avatar
  when it enters (`RoomAvatarModule.LoadBadgesAsync`, `LoadHabboClubAsync`) and pushed again
  when it changes. `PlayerSummarySnapshot` is not the way to carry it: that call is on every
  hot path. **Store the moment, not the verdict, where one exists.** `@is_hc` reads
  `IRoomPlayer.HabboClubExpiresAt` and compares it against now, so a membership running out
  while its owner stands in the room needs nobody to notice and no timer to run; only a
  purchase is pushed (subscription grain → presence → room).
- **A user variable is keyed by the avatar's room index, never by the player.** That index is
  how the client addresses any avatar and the only id a player, a pet and a bot all have; a
  player's own id is a value a variable reports (`@user_id`), exactly as `@pet_id` and
  `@bot_id` report theirs, so keying by it would confuse the address with the answer. Every
  path agrees on this: the menu passes the index the client sent, `GetTargetIds` looks the
  index up for each selected player, the fx system keys by `holder.ObjectId`, and
  `PlayerActiveStore` keeps one store per avatar and drops it on `PlayerLeftEvent.ObjectId`.
  A subclass of `UserVariable<IRoomAvatar>` then answers for all three kinds, one of
  `UserVariable<IRoomPlayer>` only where a player is meant, and `@pet_id` / `@bot_id` take
  `IRoomPet` / `IRoomBot`. Indices are handed out by a counter that only climbs while the room
  is loaded, so one is never two avatars; a player who leaves and returns is a new avatar and
  holds nothing of what the old one did.
- **Wired says "user" and means any avatar.** `IWiredSelectionSet.SelectedAvatarIds` holds
  players, pets and bots alike, by room index, and every selector gathers all three; narrowing
  to one kind is what `wf_slc_users_bytype` is for, and its mask (1 players, 2 bots, 4 pets)
  is honoured. A box reads the selection through `GetAvatars` when what it does suits any
  avatar (move, teleport, freeze, direction, hand item, a name in text) and through
  `GetPlayers` when only a player can be the subject (kick, mute, a badge, a team, a score);
  `GetPlayers` is `GetAvatars` with the others dropped, so a pet in the selection is skipped
  rather than mistaken for a player. Whatever names a player by player id — an event, a game
  team, a signal — is looked up and put into a selection as that player's avatar.
- **A trait every avatar can hold belongs on `IRoomAvatar`, implemented once on `RoomAvatar`.**
  Posture, hand item, freeze, idle, dance and effect are all declared there, and `RoomAvatar`
  carries the one implementation; a kind-specific interface declares only what that kind alone
  has (`PlayerId`, `BadgeCodes` and `AchievementScore` on a player, `Skills` on a bot, the pet's
  stats). A trait declared twice, once on `IRoomPlayer` and once on `IRoomBot`, is drift: the two
  copies drifted apart exactly that way, the player's `SetDance` refusing to *stop* a dance while
  seated and the bot's forgetting to mark the snapshot dirty. The test is what the client draws,
  not what the hotel calls a user: `RoomObjectUserTypes.getVisualizationType` maps `bot` and
  `rentable_bot` to `"user"`, so a bot dances and wears effects exactly as a player does, while a
  pet keeps its own visualization and only `AvatarLogic` reads the effect update — a pet therefore
  holds the value and shows nothing, which is a no-op and not a special case to write. A dance and
  an effect are on `RoomAvatarSnapshot` for the same reason, so room entry replays both for every
  avatar in one pass instead of branching per kind.
- **An event about an avatar names it by room index, not by player id.** `AvatarEvent` derives
  from `RoomObjectEvent` and so carries `ObjectId`, which is the address wired already selects
  and stores by; `PlayerEvent` carries a `PlayerId` and is for what happens to the *account*
  behind an avatar (entering, leaving, chatting, a controller level changing). An action —
  waving, dancing, sitting, a sign — is the avatar's, so `AvatarPerformsActionEvent` is an
  `AvatarEvent`: a bot or a pet reports it exactly as a player does, and nothing has to invent a
  player id it does not have. `CausedBy` still names the player when there is one and is the
  room's own system context otherwise. `SeedSelectionFromEvent` resolves a `PlayerEvent` through
  `AddPlayerById` and any `AvatarEvent` through `AddAvatarByObjectId`, with a general
  `case AvatarEvent` last so a new avatar event needs no wiring; the two walk-on/off cases sit
  above it only because they add their furni as well. Prefer an `AvatarEvent` for anything a bot
  or a pet could also do, and keep a `PlayerEvent` for what only an account can.
- **How a player got into the room is the room's to remember, not the client's.**
  `OpenFlatConnection` carries no reason (its third field is always -1), so a furni that sends
  someone elsewhere says so first: it puts a `RoomEntrySnapshot` on their presence against the
  room it is forwarding them to (`SetPendingRoomEntryAsync`), and the room applies it as the
  avatar is made and drops it. It is kept against one room id, so a forward the player never
  followed cannot colour a later entry, and anything else is `RoomEntryMethodType.Default`.
  `wf_act_teleport_to_room` tells the two apart by the `RoomLinkerData` it followed: a fixed
  `RoomId` is a room network, a paired `ItemId` is a teleporter and the player arrives at that
  half, which is what `@room_entry.teleport_id` reports.
- **A text connector carries a word, not a key.** The editor writes it into its table as
  `12 (Bubble Juice)`, and the client only localises a string that *begins* with `${`, so a key
  would be shown as a key. The hotel's own texts come from the client's `ExternalTexts.json`,
  named by `Turbo:Texts:ExternalTextsPath` — either a path on disk or an `http`/`https` address,
  because the file is usually served beside the rest of the client's assets. It is empty by
  default, in which case the editor simply shows the number. `IHotelTextProvider.ReloadAsync`
  reads it, called from `TurboEmulator.StartAsync` with the other providers rather than from a
  constructor, so the fetch is awaited and a hotel that names something unreadable is logged and
  started anyway: no names is cosmetic, refusing to boot is not. How long to wait on the address
  is `Turbo:Texts:FetchTimeoutSeconds`, because a timeout is a setting and not a constant. The
  request sends a `User-Agent`
  because asset hosts answer 403 to a request that names none, and `HttpClient` sends none of its
  own. `WiredTextConnectors` names hand items
  (`handitem{id}`), effects (`fx_{id}`), dances and signs by the client's own keys, following a
  text that is only a reference to another one. The provider sits in the room module because
  wired is its only caller; move it when a second module wants texts.
- **A variable with no value is held or not, never a zero.** `WiredVariableFlags.HasValue` is
  what says a variable carries a number. Without it the client only draws who holds one, and
  "has variable" is the only question anything asks of it, so an avatar or furni it is false
  for must answer that it holds nothing. `@is_hc`, `@has_rights`, `@is_owner`, `@is_frozen`,
  `@is_muted`, `@is_trading`, `@is_idle` and `@projectile.animation.is_traveling` each reported
  `0` instead, which put every avatar and every furni in the room on the holder list and left
  each flag saying nothing at all. The user and furni families answer this the same way and
  have the same three shapes: `TryGetValueForAvatar` / `TryGetValueForItem` for a variable that
  can be absent, `UserValueVariable` / `FurnitureValueVariable` for one that every target it
  binds to holds, and `UserFlagVariable` / `FurnitureFlagVariable` for one that is held or not.
  A flag declares no `Flags` of its own, because `WiredInternalVariable.Flags` is already
  `None`. A shape added to one family belongs on the other in the same change; this file used
  to say the two differ here on purpose, and the seven user flags are what that cost.
- **A projectile's flight is followed, not guessed.** The server moves furni at once and the
  client animates over the animation time it was sent, so `WiredProjectileFlight` follows that
  same clock to answer the `@projectile.animation.*` variables: where the furni looks to be,
  how far it has come, whether it is still going. The clock is the client's own; what counts
  as a collision is this server's reading, recorded on the type. A flight outlives its landing
  so a stack can read how the shot went, and is dropped when the furni leaves the room.

- User variables are keyed by the avatar's room index, like everything else wired says about an
  avatar. Every walk over "who holds a variable" goes through
  `RoomWiredSystem.GetLiveTargetIds(targetType)`: the "users with variable" selector yielded
  player ids from its own enumeration and so found nobody. Derived variables (level-up, time
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
  - Stored data is as untrusted as sent data, and a box that was never saved stores nothing.
    `FillInternalDataAsync` rebuilds every list to the shape the box declares, so code after
    it reads by index without a `try`: int params by their rules, and the definition and type
    specifics by `NormalizeSpecifics` (one entry per declared type, the default where the
    stored one is missing or does not fit). `List<object>` comes back from JSON as
    `JsonElement`, never as the `int` or `bool` that was saved; the base class used to index
    and cast inside a `catch`, which logged an exception for every freshly placed action,
    condition and selector and dropped every delay, quantifier and invert switch on the first
    room load after a save. Sources are read through `StoredOrDefault` for the same reason.
    A new kind of stored list gets its normaliser in the base class, not a `try` at each
    reader.
- A box that creates value outside the room (`wf_act_give_reward`: badges, furniture) raises
  `MinimumControllerLevelToSave`, so holding the room's wired permission is not enough to
  configure it.
- The wired menu changes variables through two packets. `WiredSetObjectVariableValue` works on
  one furni or user that is in the room right now, and its last field says what to do
  (`WiredVariableMenuOperationType`: edit a value, give the variable, take it away); each
  operation asks for the flags the client checks before it shows the button (`HasValue` and
  `CanWriteValue` to edit, `CanCreateAndDelete` to give or take), because the client is not
  trusted to have checked. `WiredDeleteAllVariableHolders` takes a stored furni or user
  variable from every holder the box keeps, present or not
  (`FurnitureWiredVariableLogic.RemoveAllValues`), and is logged. Both go through
  `RoomWiredSystem`, remove through `RemoveValue` so triggers and fx hear of it, and answer
  with the refreshed list the tab is showing.

- **A box's save packet is read from the client's composer, never mirrored from what the server
  sends it.** The two shapes differ: the client sends back only what the player can change.
  A condition's save carries one definition specific (the quantifier the player picked) and no
  type specifics, while the def the server sends carries the quantifier *type* and the invert
  flag as well. Reading those two back misaligned every condition save from the byte after the
  variable ids. A value the client only ever draws is declared by the box, not stored with it:
  `FurnitureWiredConditionLogic.QuantifierType` and `IsNegative()` feed
  `GetTypeSpecifics()` directly. Today only actions (the delay), conditions (the quantifier)
  and selectors (filter and invert) send a specific at all, and none of them sends a type
  specific.
- **A wired code is checked against the client's code table, both halves.** An editor class
  declares its own `code` and its negative twin's `negativeCode`, so `wf_cnd_not_wearing_b`
  has a code of its own (22) and must not reuse the positive box's (11), or the client opens
  the wrong editor. `scripts/wiredgap.py` reads both getters and reports any code two boxes
  claim; a second furni that is genuinely the same box inherits its twin instead of declaring
  the code again (`WiredTriggerAtTimeLong`).
- **An option whose editor greys out an input does not need that input.** The "says something"
  trigger's third mode is "Match all text": the client disables the keyword box, so the trigger
  fires on anything said. Reading it as "every word of the keyword must appear" meant it never
  fired, because the keyword is empty.
- **A sentinel is a value the rule has to allow.** The condition-evaluation addon writes -1 in
  its mode param when the player picks one of the counted modes, and puts which one in the next
  param and the number after that. A rule of `0..6` refused the save outright, and the modes
  were read one slot early. Check an editor's `readIntParamsFromForm` for a branch that
  rewrites a param before trusting the obvious reading of it.
- **A request that is not a save is not answered with a save.** `WiredSaveSuccess` closes the
  editor, so "apply furni to set conditions" (`ApplySnapshot`, which re-captures a box's furni
  snapshot through `FurnitureWiredLogic.ApplyFurniSnapshot`) answers nothing at all: the client
  expects no reply and keeps the editor open.
- Which boxes the server still lacks is measured too: `python scripts/wiredgap.py` (the
  deobfuscated client is found through `HABBO_CLIENT`) lists every wired code the client has an
  editor for and no box returns. The client knows codes, never furni names; the furni a code
  belongs to is looked up in the hotel's `furniture_definitions`. Do not write a box for a code
  no furni in the hotel carries: nobody can place it, and its param layout goes untested.
  Still missing after this pass, each for want of a system rather than a box: everything
  around chests, transactions and contracts (triggers 25 and 26, actions 45 to 48, conditions
  45 and 46, addons 18 and 20, the `wf_storage_*` and `wf_contract_*` furni), reward tracks
  (58, 59), achievements (the enabler addon 2001 and action 51, which progresses one) and the
  web API addon (2002). Give effect (52), override height (53), the level condition (44), the
  global placeholder (2000) and variable box 8 have no furni in the hotel.
- What an editor class does not say is in two other places, and both are read before a box is
  written or called unknowable. The hotel's texts and furnidata
  (`nitro-assets/gamedata/ExternalTexts.json`, `FurnitureData.json`): the `usage_info` and
  option texts say what a box does, and the furni's display name says which code it is.
  `wf_xtra_rotate_to_dir` is "Projectile" (21) and `wf_xtra_mov_curve` is "Movement Curve"
  (22, the code table's JUMP_STRENGTH); neither name gives it away. And the editor's pictures:
  the projectile's four directional systems are explained only by
  `wired_misc_directional_system_N.png`, and `WiredDirections` was read off those tile by tile.
- The projectile addon is partly done, and says so on the class: turning a projectile to its
  flight direction, a curved trajectory and overshooting are in; scaling the animation time
  with distance, the shooter's cosmetic direction and bunny hop, and the seven internal
  variables are not, because nothing the server can read says how they work. Its nineteen
  params are all declared anyway, so a save comes back as it was made. It speaks only for the
  furni it picked as projectiles (`WiredProjectileSettings.ProjectileIds`), unlike the rest of
  the policy, which covers the whole stack.
- Variable ids that mean something by position (fx: override min, override max, audience;
  place furni: spawn variable, value variable; projectile: time, distance) set
  `HasPositionalVariableIds`. The base then keeps every slot and stores "none" as id 0; without
  it an unknown id is dropped and the next variable slides into its place.
- A movement's arc is the stack's, not the action's: `wf_xtra_mov_curve` (the client's "jump
  strength") sets `IWiredPolicy.JumpStrength`, and `WiredExecutionContext` writes it into every
  move of the stack as the jump power of a user and the curve strength of a furni. An action
  never sets either.
- A value-or-variable input is read one way: the switch as a bool, then the literal, or the
  variable through `TryReadVariableOperand` (the first selected target that holds it).
  `TryResolveOperand` is that plus a literal stored as a long; a box whose editor stores one
  int reads the literal itself (`WiredAddonJumpStrength`).
- What a box does only in the client is sent to the selected players and nothing is kept:
  `wf_act_click_conf` sends `WiredClickSettings`, the client applies it and forgets it on
  leaving the room. It goes out with `LogAndForget`, like everything a box sends from the tick.
- Two placeholder addons that differ in whose names they print share
  `FurnitureWiredNamePlaceholderLogic`. A furni's name on the server is its definition name;
  the localised name is the client's.

### Variable fx
- A variable fx draws a wired variable over whoever holds it: a bar, hearts, a level badge or a
  number over avatars (user fx) or floor furni (furni fx). One addon per category
  (`wf_xtra_var_fx_health`, `_progress`, `_level`, `_status`, `_boss`, `_number`; wired codes
  1200 to 1205), all deriving from `FurnitureWiredVariableFxLogic`. The logic names are this
  server's and differ from the furnidata names (`wf_xtra_varfx_hp`, `_prog`, `_levelling`,
  `_status`, `_boss`, `_number`), unlike every other wired box, whose logic is its furni name;
  the `logic` column of those six `furniture_definitions` rows has to be set to match.
- An fx shows nothing until three things hold, and none of them is a packet: a variable box
  stands on the fx box's own tile, the box's source (user or furni) is what that variable
  hangs on, and someone in the room actually holds the variable. An fx stacked on a trigger,
  or standing alone, is configured correctly and draws nothing.
- The editor never picks the variable an fx shows, only the two that may replace its value
  range and the one that gates its audience. Which variable it shows is the room's rule: the
  variable box on the same tile (`GetVariableBoxOnTile`, shared with the sub-variable addons),
  and only when that variable hangs on what the fx is drawn over. An addon that needs another
  kind of neighbour on its tile asks `FurnitureWiredAddonLogic.GetLogicOnTile<T>()`; do not
  write the tile walk out again. A levelling fx takes level,
  level cap and the experience bounds of the current level from the level-up addon on that
  tile (`WiredAddonVariableLevelUp.TryGetLevelProgress`).
- The box holds no state and sends nothing. It answers three questions: how the fx looks
  (`BuildConfig`), what one holder shows (`ResolveStatus`) and who may see it (`Visibility`).
  `RoomVariableFxSystem` (`Turbo.Rooms/Wired/VariableFx/`) does the rest: after something
  changed it works out, per player, every status that player should be seeing, compares it
  with what they were sent and sends the difference as one ordered batch. Do not send an fx
  message from a wired box or on a variable write; mark the system dirty with an event.
- The client filters nothing, so audience is decided here (`VariableFxVisibilityType`): only
  the holder, the holder's game team, everyone, or viewers who hold a user variable (with a
  given value). It also keeps nothing across rooms: configs and statuses go out again on every
  entry, configs first.
- What the client does not check, the server must get right, because the failure is an
  exception in the client, not a missing bar. All of it was read from the AS3 and is kept in
  `VariableFxStyles` / `VariableFxIcons`, the team colours of the "delegated colour" included
  (`VariableFxStyles.GetTeamColor`); the system and the boxes hold no client table of their own:
  - a (category, style) pair outside the style table loses the whole config message;
  - a renderer must be one the style registers, a number display needs its `design` extra, an
    icon must be one the client has an asset for;
  - every status of a levelling fx, and of any fx using the levelling colour, must carry
    `is_maxed` (with `current_level` and `max_level`);
  - the two ends of a range override travel as a pair, so one overridden end repeats the
    default for the other; a maxed level sends both ends equal with `is_maxed`, which is how
    the client knows to draw the bar full.
- The client drops a status for an avatar or furni it has not been told about, and never asks
  again. Those objects reach it over the room stream while fx messages go to the player
  directly, so a new player, avatar or furni waits `WiredConfig.VariableFxEntryDelayMs` before
  anything about it is sent. A viewer's first batch, and a holder's first status, are marked
  "initialize": the client then draws the value without the change animation.
- The three variable ids of an fx box are positional (override min, override max, audience).
  `GetValidVariableIds` keeps the slots and stores "none" as id 0, which `WiredDataSerializer`
  writes as an empty string; the default implementation drops unknown ids and would slide the
  audience variable into the first slot. Any box with positional variable ids needs the same.
- Limits are `WiredConfig.VariableFx*`: boxes per room (enforced through
  `IRoomPlacementLimit`), statuses per viewer, the flush interval and the entry delay.

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
  `GameScoreChangedEvent`, `GameTeamChangedEvent`): a counter furni starts a game, wired boxes
  join teams and give points, wired triggers listen, and banzai or freeze furni will use the
  same system. Joining or leaving a team is also what tells the client it is playing
  (`YouArePlayingGame`), so that is sent from the game system and nowhere else; the variable fx
  system learns of team changes from the event, not from whoever moved the player. Frozen is
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
  how the room is told (wired batches a `WiredMovements` packet), stay with the caller. A
  `RemoveAvatar` / `SetPosition` / `AddAvatar` sequence anywhere else is drift: six copies (bot
  and pet moves, bot teleport, mount, dismount, ride sync) skipped the furni notifications and
  the invoke. The ridden pet passes `notifyFurni: false` so one step does not trigger twice.
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
  `AvatarPerformsActionEvent` carries an `AvatarActionType` and the expression, dance, sign or
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
- **The presence grain is the hub, so its methods come in two kinds** (the rule is written on
  `IPlayerPresenceGrain` too). Nearly every grain awaits the presence, and the presence awaits
  the room, the player, the messenger and the inventory — so it closes a cycle with almost
  anything. A deadlock sweep found cycles through it with the room, the player grain, the
  messenger, the inventory, the group grain, Builders Club and the wallet, all live.
  - **Tells** — sends, `On*` notifications, reads of a field (`GetActiveRoomAsync`),
    `SetPendingRoomEntryAsync`. `[AlwaysInterleave]` on the interface, and they **never await a
    grain**: anything they pass on to a room or the messenger goes out with `LogAndForget`
    (`OnPlayerUpdatedAsync` used to await the room, which made player → presence → room → player
    a three-grain cycle through respect and mannequins). Any grain may await a tell, even while
    the presence is waiting on that grain.
  - **Flows** — entering and leaving a room, opening an inventory, the session lifecycle. Not
    interleaved; they await other grains; only handlers and the presence's own session drive
    them. No grain awaits a flow.
  - A new presence method another grain will await is a tell. If it needs to ask something, it is
    a flow, and the grain that wanted it tells instead.
  - Interleaving is safe for a tell because it does its work in synchronous stretches: it
    enqueues a composer or sets a field between awaits, it never holds an invariant across one.
    A method that checks, awaits, then acts on what it checked is not a tell, whatever it is
    called.
- **Two grains of the same type talk to each other more than any other pair.** Two friends both
  message, both log in, both accept, both remove — and every one of those was an awaited call
  from one messenger to the other, so two people doing the same thing at the same moment
  deadlocked both. A call between two instances of one grain type is an interleaved,
  memory-only tell (`OnFriendAdded`, `ReceiveMessage`, `CanBeAddedBy`); the side that starts a
  change does the database work for both, in one save.
- **A grain never calls itself through a reference.** `GetPlayerMessengerGrain(self).X` inside
  the messenger waits for a turn that can only start once the current one ends. It happened on a
  friend request to your own name. Any operation that takes another player's id needs a
  `target == self` guard when the target's grain is the same type as the caller's.
- The same one-way rule holds for every helper grain of a room: `RoomTradeGrain` awaits the room,
  so the room only ever tells it things with `LogAndForget`. Before adding an awaited call from
  grain A to grain B, check that nothing B awaits leads back to A.
- Never mark a grain `[Reentrant]` to make such a chain compile. It moves the bug from a deadlock to
  interleaved state.
- **Auditing this is mechanical; do it rather than reason about it.** Every awaited grain-to-grain
  call can be listed from the source: find the `Get*Grain(...)` accessors (and
  `SendComposerToPlayer(s)Async`, which reaches the presence grain), work out which grain the file
  belongs to — a module or a `.Partial.cs` belongs to its grain — and record whether the
  statement is `await`ed or goes out with `LogAndForget`. Pairs with an awaited edge in **both**
  directions are the candidates. A sweep of the tree found three; two are now one-way.
- **The audit is at pair granularity; safety is per method.** A reported pair is not automatically
  a bug: what matters is whether the specific method A awaits can lead back to A. Read both
  methods before acting. What *is* automatic is the reverse — a pair with no awaited edge in one
  direction cannot deadlock however either side changes later, which is why turning an awaited
  notification into a `LogAndForget` is usually the cheapest permanent fix.
- **Known mutual-await pairs, and what keeps them safe:**
  - `PlayerPresenceGrain` ↔ `RoomGrain`, `PlayerGrain`, the messenger, the inventory, the group
    grain, the wallet — every one of them awaits the presence only through a tell, which is
    interleaved and awaits nothing. That is structural now, not discipline per method; it stays
    true as long as the tell/flow split above holds.
  - Messenger ↔ messenger — interleaved tells only (see above).
  - "Safe because the other side only sends composers" was the justification for Room ↔ Presence
    for a long time, and it was not true: the presence was not free to *receive* the send while
    it was itself waiting on the room. What makes an edge safe is that the callee can run the
    call *now*, which is what `[AlwaysInterleave]` says.
- **Moving the call to the caller is the wrong half of the fix.** The one-way rule says who may
  *await* whom. It does not say the owner of the state gives up publishing what changed. A group
  grain that must not await a room still tells the room, with `LogAndForget` — the group system
  spent a while with five handlers each remembering to refresh a room after calling the grain,
  which is the same bug as building a composer in a handler. Put the push in the grain that
  changed, fire-and-forget, and say in a comment that it is not awaited and why.
- **Publish from the narrowest place every path already goes through.** Eight roster operations
  (join, approve, approve-all, promote, demote, kick, leave, block) all end in
  `AddOrUpdateMemberAsync` or `RemoveMemberAsync`, so the member's own grain and their rights in
  the homeroom are refreshed there — two call sites, not eight. Four group edits all end in
  `PublishChangedAsync`. Before adding a notification, find the funnel; if there is not one, that
  is usually the thing to fix first.

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
  - **Everyone in a room**: `RoomGrain.SendComposerToRoomAsync` (the room stream), or
    `RoomGrain.SendComposerToRoomAndForget` from room code that must not wait on it (the tick, a
    broadcast made once the answer is known). The second is the first with `LogAndForget`, not
    another route; fifteen call sites were spelling the log line out. A "local send helper" is
    one that wraps a *player* send or reaches the presence directly — those stay forbidden.
- Do not send directly to raw sockets/session transports from packet handlers.
- Active-room membership/discovery belongs to `RoomDirectoryGrain`; do not bypass it with ad-hoc room tracking.
- Grain lifetime remains Orleans-managed by default; use `[KeepAlive]` only for explicitly justified directory/manager grains.

## Packet handler extensions
When two or more handlers do the same thing around their grain call — the same grain call, the
same mapping from a snapshot to a composer, the same failure-to-composer mapping — that
behaviour goes in one extension class, not in each handler.
- **Shape:** `internal static class <Thing>Extensions` in the folder of the handlers that use it,
  in that folder's namespace. Methods extend `MessageContext` (`ctx.Send…Async`,
  `ctx.…Async`) and take the `IGrainFactory` or service they need as an argument. A class does
  not hold injected state.
- **Naming:** `Send<Thing>Async` fetches or receives a result and sends the composer to the
  sender. `Send<Thing>FailureAsync(result)` sends only the refusal and returns
  `Task<bool>`, which is whether the operation went through. A caller with more to do stops on
  `false`.
- **Guards:** if every caller would write the same guard (`PlayerId`, `RoomId`, object id), it
  goes inside the extension. The handler keeps only checks on its own payload.
- **Existing ones. Reuse them before writing a new one:**

  | Extension | Used for |
  |---|---|
  | `Catalog/CatalogPurchaseExtensions` | `PurchaseOfferAsync`, `SendPurchaseFailureAsync`, `SendBalanceFailureAsync`, `SendPurchaseErrorAsync`. Every catalog refusal goes through these, including LTD raffle errors. |
  | `Navigator/LegacyRoomSearchExtensions` | `SendLegacySearchResultAsync` |
  | `Room/RoomItemInteractionExtensions` | `InteractWithRoomItemAsync`, for any packet that is "do `FurnitureInteraction` X to this item" |
  | `RoomSettings/RoomSettingsSaveExtensions` | `ResolvePlayerFlatCategory` and `SendRoomSettingsSaveFailureAsync` |
  | `RoomSettings/RoomFilterExtensions` | `SendRoomFilterAsync`, which sends the room's whole word filter |
  | `Inventory/Badges/BadgeRequestExtensions` | `SendBadgeRequestFulfilledAsync` |
  | `Users/ExtendedProfileExtensions` | `SendExtendedProfileAsync` |
  | `Users/GuildMemberMgmtResultExtensions` | `SendGuildMemberMgmtFailureAsync` |
  | `Userdefinedroomevents/Wiredmenu/WiredVariableHoldersExtensions` | `SendWiredVariableHoldersAsync`, which lists who holds a wired variable |
  | `Userdefinedroomevents/Wiredmenu/WiredVariablesForObjectExtensions` | `SendWiredVariablesForObjectAsync`, which lists the variables one target holds |

- **Where it does not go:**
  - A one-line grain call that encodes a rule belongs in `GrainFactoryExtensions` in
    `Turbo.Primitives`, for example `SendComposerToPlayerAsync` and `HasActiveClubAsync`.
  - A parser or other pure helper belongs beside its type, for example `WallPosition.TryParse`.
  - Do not extract a single `ctx.SendComposerAsync(new X { … })`. The extension has to own a
    decision or a mapping.
- An extension is not a place for side effects on other grains. The rule that a handler does
  guard, call, report still applies. See the reentrancy notes below.

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
  - A packet handler does not wrap its body in `try { } catch (Exception) { }`: `EnvelopeHost`
    logs every packet- and event-handler failure with the handler and envelope type, and
    `PackageHandler` logs parse and context failures. A handler catches only a typed exception it
    turns into a reply (`CatalogPurchaseException` → `NotEnoughBalance`). This bullet used to
    credit `PackageHandler` alone, which never saw a handler exception: the pipeline's error
    callbacks were `(ex, env) => { }` in both registries, so every handler failure in the server
    vanished.
  - An error callback or hook defaults to logging, never to an empty lambda, and a component that
    catches on behalf of others takes an `ILogger`.
  - Never `throw` inside a `try` whose `catch` is broad. The plugin loader's "more than one plugin
    entry type" check sat inside a bare `catch` and could never fire.
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
- **A new setting looks like its siblings.** A config option carries the hotel default it ships
  with (`public int X { get; init; } = 100;`) and a summary saying what it tunes. Its key goes in
  `appsettings.json` when a hotel is expected to change it; the default is what a hotel that says
  nothing gets. `required` is for an option with **no** sensible default, which therefore carries
  none and has to be in `appsettings.json` — `CryptoConfig`'s key pair is the only one in the
  repository. This file used to say every option is `required`, generalised from `PlayerConfig`,
  and lost: two hundred options across `RoomConfig`, `WiredConfig`, `PetConfig`, `NavigatorConfig`
  and the rest carry defaults instead, and `required int X { get; init; } = 500` says both things
  at once. It is also a promise the binder does not keep: `services.Configure<T>(section)` builds
  the object by reflection, so a missing key is not an error — ten of `PlayerConfig`'s twenty-three
  `required` options had no key at all and nothing noticed.
- **The adapter files say what this file says.** `CONTEXT.md` and
  `.github/copilot-instructions.md` still sent revision work to the plugin repo after it moved
  to `Turbo.Revisions/`. When a rule here changes, grep the adapters (`CONTEXT.md`, `CLAUDE.md`,
  `CODEX.md`, `.github/copilot-instructions.md`) for the old wording in the same change.
- **A rule that lost is deleted, not left beside the winner.** This file told grains to take
  limits from handlers in one section and from their module config in another; new code picked
  either. When two rules disagree, find which one the code follows, delete the other here and in
  the adapters, and remove what the dead rule left in the code (the unused `IConfiguration`
  injections).
- **A field nobody reads is a question, not padding.** `WiredSetObjectVariableValue`'s last int
  was named `ReferenceRoomId` by guess and never read. It is the operation (set, create,
  delete), so deleting a variable from the wired menu set it to zero instead, and nothing
  failed. When a parser pops a field the handler ignores, find the composer's call sites in
  the AS3 and see what the client puts there. Likewise a header constant with no parser
  (`WiredDeleteAllVariableHoldersMessageEvent`) is a feature the client has and the server
  does not; `scripts/packetgap.py` lists them.
- **A placeholder on the wire is checked in the client.** `BadgesRank = 0` looked harmless and
  was sent for every avatar; the client draws any rank of zero or more, so everyone was "#0".
  Before leaving a `0` or `// TODO` in a snapshot a serializer writes, read what the client does
  with that value and send its "nothing" (`BadgeRanks.NONE`) instead.
- **Ask whose it is before where it fits.** New state, a new setting or a new method goes to the
  system that owns the concept, not to the one that needed it first (see **System boundaries**).
  A `Wired` prefix on something in a general class, or a general module checking for a wired
  type, is the sign it landed in the wrong place.
- **A rule written in a change is kept by that change.** The variable fx section said every
  client table lives in `VariableFxStyles` while the team colours sat in the system; the
  logic rules said state values live in `Turbo.Primitives/Furniture/` while the rentable space
  declared `STATE_FREE` / `STATE_RENTED` privately. After writing a section here, grep the new
  code for what the section forbids.
- **A name in a comment is checked like a name in code.** `VariableFxConfigSnapshot` sent
  readers to a `VariableFxUpdateFlags` that was never written. No gate resolves a `<c>` tag:
  use `<see cref>` for a type that exists, and grep for one you only mention.
- **A summary says what the method does now.** `GetRoomSettingsMessageHandler` began to send a
  settings error before `NoSuchFlat` and its summary still described the old reply. Read the
  doc comment of whatever you change the behaviour of.
- **Remove what a change orphans**: the setting nothing reads, the interface member with no
  caller, the using, the appsettings key. A setting added "for later" (`OnlineTimeMinutes`,
  `MinutesBetweenMountAttempts`) is an orphan from the day it lands: add it with the code that
  reads it.
- **A `new` member that hides a base one is drift, not a specialisation.** `RoomAvatar` declared
  `public new void SetRotation` that turned the body *and* the head, over `RoomObject`'s
  body-only `SetRotation`. Which one ran depended on the static type of the variable: the
  concrete avatar classes got both, and every caller holding an `IRoomAvatar` — the walk step,
  room entry, riding, the bot and pet modules — turned the body and left the head behind. Make
  the base member `virtual` and `override` it, so the call means the same thing everywhere, or
  give the specialised one its own name. A behavioural difference that depends on the reference
  type is invisible in review and no gate catches it.
- **A second name for an inherited method is a copy of it.** `RoomAvatar.SetHeight` was
  `RoomObject.SetPositionZ` written out again, word for word, and `IRoomAvatar` published both.
  Before adding a method to a subclass, look for what the base already calls that job.
- **State the `Users` packet does not carry is replayed on room entry.** A dance, an effect
  and a sleep are each their own update, so an arriving player is sent one per avatar that has
  one, beside the `Users`/`UserUpdate` pair in `RoomService`. `IsIdle` was on `IRoomAvatar` but
  not on `RoomAvatarSnapshot` at all, so everyone who had dozed off before you walked in was
  drawn awake until they next moved. A snapshot field that exists only to be replayed says so
  on itself, as `DanceType` and `EffectId` do.
- **A composer field is named and typed for the id it carries.** `SleepMessageComposer.UserId`
  was an `int` that all three call sites filled with `avatar.ObjectId`. The room object id and
  the player id are different numbers and the name claimed the wrong one; its siblings in the
  same folder (`DanceMessageComposer`, `AvatarEffectMessageComposer`) take a `RoomObjectId`,
  and that type is what keeps the two from being swapped.
- **A `Try...` method annotates its out parameter.** `[NotNullWhen(true)] out TItem? item` puts
  the guard in the signature, so a caller that checks the result is done. Without it the check
  becomes a convention: three of the four furniture variables wrote `|| item is null` after
  `TryGetItemForKey` and the fourth forgot, which the compiler reported as two nullable warnings
  nobody read.
- **An exception is not control flow, not even inside the method that catches it.**
  `ValidateAvatarStepAsync` threw `TurboException` to mean "this step is blocked" and caught it
  a few lines down to stop the walk. A blocked step is the ordinary end of a walk, so it is a
  guard clause and a `return`; and because the same `catch` swallowed everything else without
  logging, a real failure in the walk path looked exactly like a taken tile.
- **A stub handler has one shape.** Two hundred and seventy-one handlers answer a packet the
  server has no system for with `await ValueTask.CompletedTask.ConfigureAwait(false)` and a
  summary saying what is missing. A handler that instead keeps its body commented out, and the
  injections that body needed, is neither working code nor an honest stub:
  `GetMessengerHistoryMessageHandler` still carried the `IConfiguration` this file says handlers
  no longer take, for a grain method that was never written.
- **A buffer nothing reads is half a feature, and it does not announce itself.** The messenger
  grain fills a per-conversation history, caps it with `MaxSessionMessagesPerConversation`, and
  no caller ever reads it; `ConsoleMessageHistoryMessageComposer`, its serializer and its header
  are all in place with nothing to send them. Neither end looks wrong on its own. When adding
  state, add the reader in the same change — or when finding one, say which of the two ends is
  missing rather than deleting the one you happened to open.
- **Zero warnings is the baseline, and an incremental build hides them.** A clean tree builds
  `Turbo.Main` with no warnings at all, so any warning belongs to the change in front of you.
  MSBuild only reports warnings for projects it actually recompiles, so a project that was
  already up to date stays silent about its own: `RoomSnapshot`'s uninitialised `ModSettings`
  went unreported for as long as `Turbo.Primitives` was not rebuilt. Build to a scratch
  `-p:OutDir` (which also keeps clear of a running debugger) to see the whole tree's output.
- **A reference sample nothing compiles is a rule that rots silently.** `docs/patterns/*.cs` is
  what the load order tells every tool to start from, and no project builds it:
  `TurboCloudAiGovernanceCheck` asserts only that the three files exist. All three had drifted —
  `ServicePattern` returned a `PlayerSummary` that is `PlayerSummarySnapshot` and has been for
  some time, `HandlerPattern` showed a sealed handler with `ct.ThrowIfCancellationRequested()`
  and a null check on the parsed message, which none of the 502 real handlers does, and
  `UnitTestPattern` used `xunit` and `FluentAssertions`, neither of which is in
  `Directory.Packages.props` (there is no test project at all). Each sample now names the real
  file it mirrors; when a convention changes, change the sample in the same commit, and check
  its type names by hand because nothing else will.
- **An option is read through its config class, never by key name.**
  `pluginSection.GetValue<bool>("HotReloadEnabled")` was the only place in the repository doing
  otherwise. A string key survives a rename of the property it names, and it answers `false`
  for an absent section instead of the option's own default — so `HotReloadEnabled = true` on
  `PluginConfig` was dead. Bind the section (`section.Get<TConfig>()`) or take
  `IOptions<TConfig>`, and read the property.
- **A packet handler takes a primary constructor.** Five hundred of the five hundred and two
  already did; the two extended-profile handlers were still writing out a field, a constructor
  and an assignment. (Grains are the opposite and say so above: a grain takes a classic
  constructor, because it has a body to run.)

- **A grain pair with a call in both directions deadlocks; decide the direction and write it
  down.** Orleans grains are not reentrant, so if A is inside a call to B while B calls A, both
  wait for ever. The group system has three such pairs and every one of them was written the
  wrong way round first:
  - the room grain asks the **group** grain for a member's rank on every controller-level check,
    so the group grain may never call a room grain;
  - the group grain asks the **player's** guild grain for their membership count before a join,
    so that grain may never call a group grain;
  - the player's guild grain tells the **room** its owner's badge changed, so the room may not
    read that badge back out of the player's grain — it arrives as arguments.

  Where a push is genuinely needed in the barred direction it goes **in the handler**, after the
  grain call has returned: `GuildFurniRefreshExtensions`, `GuildRoomRefreshExtensions` and the
  settings push in `UpdateGuildSettingsMessageHandler` all exist for that reason and say so. The
  one exception in the tree is `GuildGrain.DeactivateAsync` calling `RoomGrain.OnGuildDeletedAsync`,
  and it is safe only because the group is already gone from the directory by then, so the room
  resolves no group and cannot ask the group grain anything. That ordering is load-bearing, not
  incidental.

  Before adding a call between two grains, ask what the callee already calls. If the answer is
  "me", the call belongs in a handler or a service.
- **A cache that answers "no" must be invalidated when the answer becomes "yes".** `RoomGrain`
  holds whether it is a group's homeroom, and a room that is nobody's homeroom caches that so it
  does not re-ask on every rights check. Creating a group whose homeroom was already loaded —
  the usual case, since the wizard is opened from inside it — left the room certain it was an
  ordinary room, so the owner had none of the rights their own new group had just given them.
  A negative answer needs the same invalidation path as a positive one.
- **A change to who may do something has to reach whoever is standing in the room.** Joining,
  leaving, being approved, promoted or demoted changes a player's rights in the group's
  homeroom. Nothing re-derives that on its own: the handler calls
  `RefreshGuildRoomMemberAsync`, which refreshes that one player if they are in the room.
  Without it a player keeps whatever they walked in with until they leave and come back.
- **A fixed-width wire format needs a guard where the value enters, not a comment.**
  `GuildBadgeCodes` writes a badge as six-character tokens: prefix, two digits of part, two of
  colour, one of position. `BASE_KEY_MAX`, `SYMBOL_KEY_MAX` and `COLOR_KEY_MAX` described that
  and were enforced nowhere, so a hotel that seeded a part id past 99 (or a symbol past 199)
  would have produced codes that read back as a different badge entirely. The limits are now
  checked in `GuildBadgeParts.Sanitize`, where parts arrive. Constants that state a limit but
  guard nothing are the same dead weight as an unused config key.
- **`dotnet format ... --diagnostics IDE0005` reports nothing; unused usings need a real build.**
  IDE0005 does not fire without a documentation file, and the repository sets no severity for
  it, so neither gate sees an unused using. To sweep for them without touching `.editorconfig`:
  write a `.globalconfig` (`is_global = true`, `dotnet_diagnostic.IDE0005.severity = warning`)
  and a targets file adding it as an `EditorConfigFiles` item, then build `Turbo.Main` with
  `-p:CustomAfterMicrosoftCommonTargets=<targets> -p:EnforceCodeStyleInBuild=true
  -p:GenerateDocumentationFile=true --no-incremental` and grep for IDE0005. (Adding it as a
  `GlobalAnalyzerConfigFiles` item through `CustomBefore…` is not picked up.) Verify the sweep works by adding a deliberately unused `using` first — a
  silent run means the detector is off, not that the tree is clean. Text heuristics do not
  substitute: they misfire on attributes, enum types and extension methods.
- **Dead state is a lie about what the grain knows.** `GuildLiveState.IsLoaded` and its twin on
  the player's grain were set on every load and read nowhere, which reads as though something
  distinguishes "not loaded yet" from "no such group" when nothing does. Either read the flag or
  delete it.
- **An entity-to-snapshot extension that nothing calls is worse than none.** `GuildEntityExtensions`
  grew a `ToSnapshot` for `GuildMemberEntity` while both real call sites built the snapshot from
  a column-trimmed query projection, which this file sanctions. The extension was dead and would
  have been adopted by the next person as the "right" way, quietly adding a second mapping. If
  the projection is correct, delete the extension.

- **Lazy resolution and a snapshot handed out by reference do not mix.** `RoomGrain` resolved
  which group owns it on first ask and stamped the answer onto `_state.RoomSnapshot`. But
  `GetSnapshotAsync` returns that snapshot directly and awaits nothing, so whoever asked for the
  room's listing first got one with no group on it — the room had never been given a reason to
  look. Anything a snapshot carries that is not on the row it was built from has to be resolved
  **while the room loads**, not when something first happens to ask.
- **Clearing a cached lookup means clearing everywhere it was copied to.** The same resolution is
  held twice: on a field, and stamped onto the room snapshot. Invalidating only the field left a
  failed re-read advertising a group the room could no longer confirm — no rights, but still the
  badge. If a value is cached in two places, the invalidation path has to name both.
- **Two `SaveChangesAsync` calls are two chances to half-finish.** Creating a group wrote the
  group, saved, then wrote its owner's membership and saved again. A failure between the two
  left a group nobody was in, whose owner then stood in their own homeroom with no rank and so
  no rights. Add both and save once: EF orders a principal and its dependent itself and fills
  the foreign key, as long as the dependent points at the **entity** rather than at its id. The
  same rule this file already states for delete-plus-insert applies to insert-plus-insert.
- **Money moves before the thing it buys exists; say what happens when the thing fails.**
  Creating a group charged the buyer and then wrote the rows, with nothing around the write. A
  database error took the credits and gave nothing back, and the player has no way to see it
  happened. Where a debit cannot share a transaction with what it pays for — it never can, the
  wallet is another grain — the write goes in a `try` and the failure path refunds. A refund that
  itself fails is logged and swallowed: there is no third place to put the money, and throwing
  would lose the original error.

- **Every field of a cached copy needs an invalidation path, not just the one you were thinking
  about.** A room holds its group's summary — id, **name**, **badge**, colours, type — and draws
  its navigator listing from it. Changing the group's settings refreshed it; renaming the group
  and redrawing its badge did not, because those two had been written while thinking about the
  furni they repaint, and the furni reads the directory rather than this copy. A rename left the
  room advertising the old name indefinitely. When a cache holds a record rather than a single
  value, enumerate what the record contains and check every writer of each field, not the field
  that prompted the change.
- **The unused-using sweep, and why it has to be a sweep.** Nothing in either gate sees an unused
  `using`: IDE0005 needs a documentation file and the repository sets no severity for it, so
  `csharpier`, `dotnet format` and the build are all silent. The recipe is above; run it over the
  whole tree rather than the files you touched, because removing one `using` can make another
  unnecessary — a full pass took four rounds to reach zero (107, then 7, then 2, then none).
  Re-run `csharpier format` between rounds and confirm the diff contains nothing but `using`
  lines before trusting it.

- **A handler that does anything after the grain call is a design smell.** A packet handler
  guards its inputs, calls one grain method, and turns the answer into a packet. If it is also
  refreshing a room, repainting furni or invalidating a cache, that work belongs to whatever owns
  the state — see the reentrancy section for how to push it without deadlocking. Nine guild
  handlers had grown a refresh block each; all nine are now guard, call, report.
- **Identical failure reporting in N handlers becomes wrong in one of them.** Seven roster
  handlers each spelled out which composer carries which kind of refusal, and three had grown a
  branch for a failure their own operation cannot produce. One extension
  (`GuildMemberMgmtResultExtensions`) now owns that mapping and returns whether the operation
  went through, so the one caller with more to do can stop. Repeated packet-shaping across
  handlers of the same family is worth an extension in the same folder, the way
  `ExtendedProfileExtensions` and `LegacyRoomSearchExtensions` already are. See **Packet handler
  extensions** for the shape.
- **The copy nobody updated is the one that is wrong.** A sweep for duplicated handler logic
  found drift in almost every family that had more than one copy:
  - The room-ad purchase had its own catalog error mapping. It sent `RequiresHabboClub` as the
    raw code 101, so the client showed "unknown" where it should have said club required.
  - The LTD raffle sent "not enough credits" on the refusal packet. That packet words only code 1.
  - `MoveWallItem` parsed wall positions by hand. It did not use `WallPosition.TryParse`, so
    short input threw and `z` was parsed with the server's locale.
  - `RequestABadge` left the client with no answer for an unknown request code. Its sibling
    answered "not fulfilled".

  Each family now goes through one extension or parser. Before writing a handler, look for a
  sibling that already does the same thing and call what it calls.
- **A bidirectional await can be introduced by an innocent-looking read.** `RoomAvatarModule`
  awaits the player's guild grain when somebody walks in, to learn the badge they wear. That made
  the favourite-group setter — which awaited the room, to tell it the badge changed — a deadlock
  waiting for the two to happen together. Nothing about either call site looked wrong on its own.
  When adding a read from grain A to grain B, grep what B already calls on A before deciding the
  direction is free.

- **A predicate over an enum belongs beside the enum, not at each call site.** "Is this player in
  the group" was written nine times in four spellings: `rank is (Owner or Admin or Member)`,
  `CountOfRanks(Owner, Admin, Member)`, an `[Owner, Admin, Member]` array for a query, and —
  twice — the *inverse*, `Rank != Requested && Rank != Blocked`. All nine read correctly, and the
  two written by exclusion would have disagreed with the rest the moment a rank was added,
  silently counting it as a membership. `GuildMemberRanks.IsMember` / `CanManage` are now the only
  definitions, with `MemberRanks()` / `ManagingRanks()` returning fresh arrays for queries (a
  plain array is what EF turns into an `IN`, and a fresh one cannot be edited by a caller).
  When the same question about an enum is asked in more than one file, give it a name next to
  the enum — and prefer stating it by inclusion, so a new member defaults to "no".
- **A helper duplicated across modules belongs in `Turbo.Primitives`.** Clamping client text to a
  stored length existed four times: the navigator, room settings and both guild grains. The two
  guild copies had left the `Trim()` out, so a group could be named with leading spaces where a
  room could not — the drift was already there, in behaviour, not just in line count. It is now
  `Turbo.Primitives.Texts.ClientText.Truncate`. Before writing a small string, id or number
  helper, grep for the body, not the name: these four were spelled `Truncate`, `Truncate` and
  `Clamp` twice.
- **A mapping that takes resolved values invites the resolution to be repeated.**
  `ToSummarySnapshot(entity, hasForum, primaryColor, secondaryColor)` made each of its three
  callers write out the same two palette lookups. Taking the palette itself
  (`ToSummarySnapshot(entity, palette, hasForum)`) keeps the mapping a pure function — a snapshot
  is data, not a provider, so the rule above still holds — and leaves one copy of the lookup.
  Pass the **source** a mapping needs, not the answers, unless the caller genuinely knows
  something the mapping cannot.

- **"Which subscription counts as club" is one decision, so it has one call.** Two grains each
  wrote `GetPlayerSubscriptionGrain(x).HasActiveAsync(SubscriptionType.HabboClub, ct)`, with
  different signatures for the same question — one taking a player id, one using its own. It is
  now `IGrainFactory.HasActiveClubAsync(playerId, ct)`, beside `SendComposerToPlayerAsync` in
  `GrainFactoryExtensions`, which is where a one-line grain call that encodes a rule belongs.
- **An awaited call that is safe "because of the order things happen in" is worth not awaiting.**
  `GuildGrain.DeactivateAsync` awaited the room to hand back the homeroom's furni, and it was
  genuinely safe: the group is out of the directory by then, so the room resolves no group and
  cannot ask the group grain anything. But that safety is a property of the room's furni-removal
  path, which is somebody else's to change. It is `LogAndForget` now, and the pair cannot deadlock
  whatever either side does later. Prefer removing the edge to documenting why the edge is fine.

- **An offer's price is read in one place.** `CatalogOfferSnapshot.ToDebitRequests(quantity)` is
  what buying it takes out of a wallet. The LTD raffle had its own copy that left out silver, so
  a silver-priced LTD was handed out for free. Do not build `WalletDebitRequest`s from an offer
  by hand; `CurrencyKind.Credits` / `Silver` / `ActivityPoints(type)` name the kinds.
- **Who may buy an offer is checked on every path that sells it.** `CatalogOfferSnapshot.
  RequiresClub` (club level above none; this client collapses the tiers, so any active
  membership meets it) is checked by the shop purchase, the room ad and the LTD entry. The level
  was sent to the client and checked nowhere, so a club-only offer could be bought by anyone who
  sent the packet.
- **Every "charge, then create" path refunds through `IGrainFactory.RefundAsync`.** Group
  creation refunded; the catalog grant, the room ad and the LTD draw did not, so a failed write
  after a successful debit kept the money. The debit can never share a transaction with what it
  pays for, so: debit, then `try` the creation, and on failure (thrown *or* refused) refund and
  rethrow. Only wrap what the money bought — a failure after the thing exists (a notification, a
  subscription extension that tells the player) must not refund a purchase that landed.
- **Every path to a state checks the same limit.** Joining a group checked the member cap;
  approving one request and approving the lot did not, so an exclusive group could be approved
  past it. The limit is now a question with a name (`GuildGrain.FreeMemberSlots`) that all three
  ask. When you find a limit checked in one path, list every other way to reach that state.
- **A count the client sends is read with `PopList` / `PopCount`, never `PopInt`.**
  `PopList(bytesPerItem, read)` bounds the count by what the packet can hold and reads the
  entries; twelve parsers had the loop by hand, and two used a raw `PopInt` — one of them sized a
  dictionary from it, so one packet could ask the server to allocate a billion entries.
- **A row two packets carry is written by one serializer.** The inventory and the trade window
  both start an item with id, type and *ref*, and the client locks an inventory item while it is
  in a trade by matching that ref. Inventory wrote a floor item's ref negated, trade wrote it
  positive, so no floor item was ever locked. `FurnitureItemSerializer.WriteHead`,
  `BadgeEntrySerializer` and `OwnerNamesSerializer` exist for that reason; when a second
  serializer needs a structure, move it to `Data/` before writing it again.
- **Text the client prints as-is has one format.** `ClientDates.Format` (dd-MM-yyyy) for every
  date the client shows, `HexColor.IsRgb` for colours, `ClientText.Truncate` for lengths. Four
  copies of the date format existed and the profile used a fifth; two colour checks disagreed on
  whitespace.
- **A reload replaces, it does not clear and refill.** A provider that clears its maps and then
  loads is empty after a failed reload; build the new maps and swap them in
  (`FurnitureDefinitionProvider`, `CurrencyTypeProvider`).
- **A special case for one item behaves like the general case.** `GetPlayerNamesAsync` answered
  `""` for an unknown id when asked about one, and left it out when asked about several. If a
  fast path is worth having, it returns what the slow path would.
- **Stop releases everything start acquired.** `NetworkManager` started a TCP and a WebSocket
  host and stopped only the TCP one. Review a start and its stop side by side.
- **A value computed and then not used is a bug report, not dead code.** The plugin table-prefix
  code computed a prefix and returned the raw one, so a plugin with none got `""` — and
  uninstalling it ran `DROP TABLE ... LIKE '%'` over the whole database. `MigrationHelper` now
  refuses an empty or non-identifier prefix. Before deleting an unused value, find what it was
  meant to protect.

- **A later optional wire field forces the earlier one to be written.** The client's chat parser
  reads a receiver index if bytes remain, then a bubble width if bytes remain; the serializer
  wrote the width only when there was a receiver, so bot bubble widths never arrived. Write the
  client's "unset" value (-1) for the earlier field instead of skipping it.
- **A separator in a wired string param is whatever the client's `readStringParamFromForm`
  joins with.** The name selector split on `/`; the client sends tabs, so a list of names matched
  nobody. Read the editor class before parsing its string.
- **Who may use an item is `IFurnitureLogic.CanUseAsync`, not the usage policy alone.** The
  policy is also what the client draws a use button for, so an item whose use is one person's
  (a monster plant seed, a pet package: the client offers them to the owner and sends a plain
  use) keeps its policy at Nobody and overrides `CanUseAsync` to `IsItemOwner`. Both used to
  have real `OnUseAsync` bodies behind a Nobody policy that refused every use, so planting a
  seed and opening a pet package did nothing. A usage-Nobody logic that does not override
  `CanUseAsync` never has `OnUseAsync` called — do not give it one.

- **A wired box moves a floor furni with `IWiredExecutionContext.TryMoveFloorItemAsync`.** It
  asks `CanPlaceFloorItem` and then moves, in one call. Ten movers each wrote the check and the
  move out by hand, "furni to furni" and "move furni to" were the same loop (now
  `FurnitureWiredActionLogic.MoveOntoTargetFurniAsync`), and the copies had drifted: chase pinned
  the furni's height where its mirror, flee, let the room's move physics decide, and the two
  twins offered different sources for their target slot. Height is the policy's (the
  `KeepAltitude` addon), not the box's; pass a `z` only when the box itself sets one (match to
  snapshot, placement variable).
- **A room writes a furni's placement through `WritePlacement`.** `FurnitureEntity` and
  `BuildersClubFurnitureEntity` share their placement columns through `IPlacedFurnitureEntity`,
  and the persistence grain marks exactly those modified, by name, for both. Two hand-written
  copies of that block had already grown apart (one marked the room column three times).

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
