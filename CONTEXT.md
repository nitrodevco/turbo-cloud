# Turbo Cloud Architecture Context

`Turbo.Cloud.sln` is the main emulator solution. `Turbo.Main` is the runtime host and composition root.

## Core structure and responsibilities
- `Turbo.Main/`
  - Host startup and wiring (`Program.cs`), configuration, lifetime, and console commands.
- `Turbo.Plugins/`
  - Runtime discovery, loading, start/stop, and unload lifecycle for plugins.
- `Turbo.PacketHandlers/`
  - Incoming message handling orchestration and domain dispatch.
- `Turbo.Events/`
  - Event behavior/handler pipeline registration and execution.
- `Turbo.*` domain modules (`Rooms`, `Players`, `Catalog`, `Inventory`, etc.)
  - Domain services, snapshot providers, and Orleans grain orchestration.
- `Turbo.Database/`
  - EF Core context and persistence infrastructure.
- `Turbo.Primitives/`
  - Cross-module contracts, identifiers, snapshots, and message types.

## Hard boundaries
- Keep host composition and module registration in `Turbo.Main`; avoid leaking host concerns into domain modules.
- Keep packet handlers focused on request/response orchestration, not persistence infrastructure wiring.
- Keep database querying and persistence access out of packet handlers.
- Keep grain lifecycle/state logic within grain modules; do not bypass grain boundaries with direct cross-layer shortcuts.
- Keep plugin lifecycle operations inside `Turbo.Plugins`; do not duplicate plugin loading logic in unrelated modules.
- Protocol revision parser/serializer trees are owned by the plugin repo at:
  - `../turbo-sample-plugin/TurboSamplePlugin/Revision/**`
  - Do not create `Revision<id>/Parsers` or `Revision<id>/Serializers` trees in `turbo-cloud`.
- Extended profile flow boundary:
  - `Turbo.PacketHandlers/Users/*ExtendedProfile*Handler.cs` orchestrates lookup + response mapping only.
  - `Turbo.Players/Grains/PlayerDirectoryGrain.cs` owns username/id lookup semantics and cache coherence.
  - `Turbo.Players/Grains/PlayerGrain.cs` exposes profile snapshots consumed by handlers.

## Data and lookup semantics
- Username-to-id lookup behavior is case-insensitive.
- Directory cache updates must keep forward and reverse mappings consistent across set/invalidate paths.
- Avoid adding handler-level fallbacks that bypass directory-grain lookup responsibilities.

## Session and room runtime flow
- Connection accepted:
  - session is added to gateway/session tracking.
- After SSO success:
  - session is registered to `PlayerPresenceGrain` for that player id.
- Player outbound targeting:
  - resolve target player's presence grain
  - call `SendComposerAsync`
  - fan-out to subscribed sessions happens inside presence flow.
- Room activity:
  - active room indexing/lookup and keepalive ping responsibilities stay in `RoomDirectoryGrain`.
- Lifecycle:
  - inactive grains are Orleans-managed and can deactivate automatically unless explicitly marked `[KeepAlive]`.

## Grain runtime patterns
- Every grain that does cross-grain calls or DB work must inject `ILogger<T>` and log caught exceptions. No bare `catch { }`.
- Independent grain calls (e.g. checking online status for N friends) must use `Task.WhenAll`, not sequential `await` in a loop.
- Identical grain calls must not repeat inside loops — hoist before the loop.
- DB batch operations use single `WHERE ... IN (...)` queries, not per-entity `ExecuteDeleteAsync` loops.
- Housekeeping writes (e.g. delivered flags) follow the timer-flush pattern: queue dirty state, flush with `RegisterGrainTimer`, flush on `OnDeactivateAsync`. See `RoomPersistenceGrain` for reference.
- Do not hardcode limits (`Take(N)`, capacity constants) in grains. Pass them from handlers via `IConfiguration`.
- When a delete + insert must be atomic, use EF tracked operations (`Remove` + `SaveChangesAsync`), not `ExecuteDeleteAsync`.
- Replace `.Ignore()` on grain tasks with a `LogAndForget` helper that logs faulted continuations.
- In-memory collections that grow per-event (message history, queues) must have a configurable cap.

## Placement rules
- New host startup/wiring behavior:
  - `Turbo.Main/` (usually `Program.cs`, `Extensions/`, or `Console/`)
- New incoming packet behavior:
  - `Turbo.PacketHandlers/<Domain>/<Name>MessageHandler.cs`
- New domain service/provider:
  - `Turbo.<Domain>/...` in the existing service/provider structure
- New grain behavior:
  - `Turbo.<Domain>/Grains/...`

## Pattern references
Use and adapt these examples before inventing new structure:
- `docs/patterns/ServicePattern.cs`
- `docs/patterns/HandlerPattern.cs`
- `docs/patterns/UnitTestPattern.cs`
