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
