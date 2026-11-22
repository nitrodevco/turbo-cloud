# Pattern Samples

Reference implementations used for AI prompts and contributor consistency.
Start from these shapes and adapt to the target module.

- `HandlerPattern.cs`: packet handler shape — mirrors
  `Turbo.PacketHandlers/Navigator/CanCreateRoomMessageHandler.cs`
- `ServicePattern.cs`: domain service orchestration shape — mirrors `Turbo.Navigator/NavigatorService.cs`
- `UnitTestPattern.cs`: edge-case-first unit test shape

**No project compiles these files.** `TurboCloudQualityGate` checks only that they exist, so a
type they name can be renamed or deleted and nothing will say so — which is how the previous
versions came to return a `PlayerSummary` that has not existed for some time, and to show a
handler shape (sealed, `ct.ThrowIfCancellationRequested()`, a null check on the parsed message)
that none of the 502 real handlers uses. Each sample now names the real file it mirrors: read
that file, not just the sample, and when you change a convention, change the sample in the same
commit.

`UnitTestPattern.cs` mirrors `Turbo.Tests/Furniture/DimmerStatesTests.cs`. Tests live in
`Turbo.Tests` (xUnit, run with `dotnet test Turbo.Tests/Turbo.Tests.csproj`); its `Support/`
folder builds grains, rooms and packet round-trips without an Orleans silo or a database (see
`Turbo.Tests/Rooms/RoomStackingTests.cs` and `Turbo.Tests/Protocol/LatencyPingTests.cs`).
The helper quick reference is [Core Test Helpers](../rules/testing.md); the verification loop is
in `AGENTS.md` under `## Tests`.

## Task recipes

What to read and what not to change for the common kinds of change. The rules themselves are in
`AGENTS.md` and the `docs/rules/` page for the area.

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
