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
Handlers already read configuration values (e.g. `Turbo:FriendList:UserFriendLimit`) from `IConfiguration` and pass them to grains.
Magic numbers like `Take(50)`, `Take(20)`, or `maxIgnoreCapacity = 100` must come from configuration parameters on the grain interface method.
A 10,000 user hotel needs different tuning than a 10 player dev server.

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
  `SendToPlayerAsync`; room-wide changes go through `SetLegacyDataAsync` / `SetNumberDataAsync` /
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
- A trade is room state (`RoomTradeModule`, `TradeSession` in `RoomLiveState.TradesByPlayerId`):
  both parties are avatars in the room and leaving ends it. Offers hold inventory snapshots;
  nothing is reserved, so the commit re-checks and moves each side through
  `IInventoryGrain.TransferFurnitureAsync` (one owner-change statement per side, then the
  receiving inventory is told). A half-failed commit is handed back and closed with
  `TradeCloseReasonType.CommitError`.
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
  bounded by `RoomConfig.WiredMaxDepth`), clocks (`WiredClockTickEvent` from
  `FurnitureCounterClockLogic`), games and scores (`RoomWiredSystem.Game`), variable writes
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
  the variable boxes. Value timestamps live in `KeyValueStore.Timestamps`.
- Team, freeze and game effect ids are `RoomConfig` tunables (`WiredTeamEffectIds`,
  `WiredFreezeEffectIds`); the freeze list ships as zeros and is hotel data.

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
- Player-targeted outbound flow must be:
  - resolve player presence grain
  - call `SendComposerAsync`
  - rely on presence fan-out to subscribed sessions
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
  existing one first. `WiredMaxDepth` existed unused while a second depth setting was added beside
  it. An unused setting is a signal to wire it up or delete it, not to add a sibling.
- **One way to do a thing.** If a change makes two members do the same job (two selection
  methods, two eviction paths, two ways to build an inventory item), collapse them in the same
  change and move every caller. Aliases kept "for compatibility" inside this repository are drift.
- **Sibling features share a shape.** Inventory sections, wired box kinds, pet and bot modules:
  when one gets a capability (batching, a `GetAsync`, a delete), give it to its siblings or say
  why not. Behaviour lives in the module; grain partials forward.
- **Batch across grain boundaries.** A loop that makes a grain call per item is a bug in waiting.
  Pass the whole set (`OnFurnitureAddedAsync`, `AddFurnitureFromRoomItemSnapshotsAsync`), group
  by target grain, and run independent targets with `Task.WhenAll`.
- **One public type per file, in the folder for its kind** (see Type placement). A base class and
  its two registered subclasses are three files.
- **No silent catch, no blocking wait.** Both hide failures that only show up under load.
- **A claim about the client is checked in the client.** Behaviour attributed to the Flash client
  (what it sends after `CloseConnection`, what a param means) is read from the AS3 source before
  it is written into code or a comment. Where the source only has localisation keys, the
  assumption is recorded on the enum.
- **Remove what a change orphans**: the setting nothing reads, the interface member with no
  caller, the using, the appsettings key.
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
