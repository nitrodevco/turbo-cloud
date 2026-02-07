# Orleans in Turbo Cloud

## Quick Mental Model
Turbo Cloud runs Orleans in-process with the emulator host. Domain behavior is modeled as grains (players, rooms, catalog, inventory, directories), and app services/packet handlers call grains through `IGrainFactory`.

Think of the runtime flow as:
1. `Turbo.Main` boots host + Orleans silo.
2. Network/message pipeline resolves a request.
3. Handler/service calls one or more grains.
4. Grains update state, coordinate with other grains, and emit outbound effects.

## Core Concepts for New Contributors
- A grain is not a thread. It is a virtual actor instance keyed by identity (for example player id or room id) with serialized execution per activation.
- Grains are activated on demand and can be collected when idle unless explicitly kept alive.
- Grains are best for stateful, keyed domain logic. Regular services are best for stateless orchestration and infrastructure.
- Keep transport/session concerns decoupled from domain decisions; grain state should model domain truth, not socket mechanics.
- Prefer shared constants for Orleans storage names, stream providers, and state keys instead of hardcoded strings.

## Where Orleans Is Configured
Orleans is configured in `Turbo.Main/Extensions/HostApplicationBuilderExtensions.cs` and invoked from `Turbo.Main/Program.cs`.

Current configuration includes:
- `UseLocalhostClustering()`
- Silo endpoint: `127.0.0.1:11111`
- Gateway endpoint: `127.0.0.1:3000`
- Grain collection age: 2 minutes (`GrainCollectionOptions.CollectionAge`)
- In-memory grain stores:
  - `PubSubStore`
  - `PlayerStore`
  - `RoomStore`
- In-memory stream providers:
  - `DefaultStreamProvider`
  - `RoomStreamProvider`

These names are centralized in:
- `Turbo.Primitives/Orleans/OrleansStorageNames.cs`
- `Turbo.Primitives/Orleans/OrleansStreamProviders.cs`
- `Turbo.Primitives/Orleans/OrleansStateNames.cs`
- `Turbo.Primitives/Orleans/OrleansStreamNames.cs`

## Storage and State in This Repo
Persistent grain state is defined with `[PersistentState(...)]` and named via shared constants.

Example from `Turbo.Players/Grains/PlayerPresenceGrain.cs`:
- State name: `OrleansStateNames.PLAYER_PRESENCE`
- Storage provider: `OrleansStorageNames.PLAYER_STORE`

This grain stores session/player presence details such as active room, pending room state, and session key.

## Streams in This Repo
Room outbound fan-out uses Orleans streams in `PlayerPresenceGrain`:

1. On room enter (`SetActiveRoomAsync`):
   - Resolve provider `RoomStreamProvider`
   - Build stream id using `OrleansStreamNames.ROOM_STREAM`
   - Subscribe the grain (`SubscribeAsync(this)`)
2. On room leave (`ClearActiveRoomAsync`):
   - Unsubscribe from the stream
3. On stream message (`OnNextAsync`):
   - Filter excluded players
   - Queue composer payload to session observer transport

This pattern separates room event publication from per-player delivery concerns.

## Snapshots: When and Why
Use snapshots when grain state needs to cross boundaries:
- Sending data out of grains to sessions/transports.
- Returning data from grains to handlers/services.
- Sharing read models without exposing mutable internal state.

Guidelines:
- Keep snapshot types small and explicit to the outbound contract.
- Do not expose mutable grain state objects directly.
- Version snapshot shapes deliberately when external consumers depend on them.

## Request Flow Examples
### Example A: Catalog purchase
`Turbo.PacketHandlers/Catalog/PurchaseFromCatalogMessageHandler.cs`:
1. Handler receives incoming packet.
2. Calls `_grainFactory.GetCatalogPurchaseGrain(ctx.PlayerId)`.
3. Invokes purchase grain method.
4. Returns success/failure composer to session context.

### Example B: Player enters/leaves room
`Turbo.Players/Grains/PlayerPresenceGrain.cs`:
1. Grain updates persistent presence state.
2. Coordinates with room/player directory grains.
3. Subscribes/unsubscribes room outbound stream.
4. Bridges stream outputs to session observer.

## Session and Presence Flow (High Level)
The current architecture intentionally decouples sessions from player identity:
1. A connection is accepted and tracked by session infrastructure.
2. Authentication binds the session to a player presence grain for that player id.
3. Outbound payloads addressed to a player are pushed through player presence.
4. Active sessions subscribed to that player receive the payload.

This model supports multiple live sessions per player identity while keeping domain behavior player-centric.

## How Plugins Fit with Orleans
Turbo plugins are hosted by `Turbo.Plugins/PluginManager.cs`. Plugins can still use Orleans normally by resolving host services (including grain access paths) through DI.

Plugin lifecycle note:
1. Plugin service provider is built.
2. Plugin DB migration hook (`IPluginDbModule`) runs.
3. Hosted services + plugin start methods execute.

Orleans remains the core domain runtime for shared emulator state and cross-feature coordination.

## Extension Guide
### Add a new grain-backed feature
1. Define grain interface and contracts in `Turbo.Primitives` (or the relevant shared abstraction project).
2. Implement grain in the owning module (for example `Turbo.Players/Grains`, `Turbo.Rooms/Grains`, etc.).
3. Choose state strategy:
   - No persistent state (stateless/transient behavior), or
   - `[PersistentState(...)]` with the correct named storage provider.
4. If event fan-out is needed, choose stream provider/name constants and implement subscription lifecycle.
5. Call the grain via `IGrainFactory` from packet handlers/services.

### Grain vs regular service (rule of thumb)
- Use a grain when behavior is keyed entity state or needs serialized per-entity concurrency (player, room, inventory domain object).
- Use a regular service for stateless orchestration, message translation, or infrastructure concerns.

## Cleanup and Lifecycle Test Checklist
When changing sessions, authentication, player presence, or room presence, test at least:
1. Connect then disconnect before auth: session cleanup leaves no stale subscriptions.
2. Connect then successful auth: player presence registration happens once.
3. Multiple sessions on same player id: targeted outbound reaches all expected active sessions.
4. Session disconnect after auth: player presence and stream subscriptions are removed/updated correctly.
5. Room enter/leave transitions: room stream subscribe/unsubscribe behavior is correct.
6. Idle behavior: activations can collect when expected and keep-alive behavior matches intent.
7. Reconnect flows: no duplicate registrations and no ghost presence after repeated connect/disconnect.

## Common Pitfalls
- Assuming in-memory Orleans providers are durable production persistence.
- Forgetting to unsubscribe stream subscriptions on lifecycle transitions.
- Mixing transport/session concerns directly into domain state logic.
- Hardcoding stream/storage names instead of using `Turbo.Primitives/Orleans` constants.

## File Map
- Host bootstrap: `Turbo.Main/Program.cs`
- Orleans host config: `Turbo.Main/Extensions/HostApplicationBuilderExtensions.cs`
- State/storage/stream constants: `Turbo.Primitives/Orleans/*.cs`
- Player presence grain: `Turbo.Players/Grains/PlayerPresenceGrain.cs`
- Catalog handler grain usage: `Turbo.PacketHandlers/Catalog/PurchaseFromCatalogMessageHandler.cs`
- Plugin lifecycle integration: `Turbo.Plugins/PluginManager.cs`
