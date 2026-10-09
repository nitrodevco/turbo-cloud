# Claude Adapter (Turbo Cloud)

This adapter points Claude to the canonical AI contract for this repository.

## Required context load order
1. `AGENTS.md`
2. `CONTEXT.md`
3. One relevant sample in `docs/patterns/`
4. `.github/copilot-instructions.md` (tool adapter parity rules)

## Non-negotiable constraints
- Keep packet handlers orchestration-only.
- Do not query database contexts/repositories from packet handlers.
- Do not send composers directly to sockets/sessions from handlers. There are exactly three ways
  to send one: `ctx.SendComposerAsync` to the session being handled,
  `grainFactory.SendComposerToPlayerAsync` / `SendComposerToPlayersAsync` to a player, and
  `RoomGrain.SendComposerToRoomAsync` to a room (or `RoomGrain.SendComposerToRoomAndForget`,
  its fire-and-forget form, from room code that must not wait). Do not spell out
  `GetPlayerPresenceGrain(id).SendComposerAsync(...)` or add a local send helper.
- For `Revision<id>` parser/serializer work, edit `Turbo.Revisions/Revision<id>/**`.

## Tests
- A change to behaviour comes with a regression test in `Turbo.Tests` that fails without the
  change and passes with it. Drive the entry point that production uses (the grain or module
  method, or client bytes through `PacketHarness`) and assert what a player or the client would
  observe, not private fields. `Turbo.Tests/Support` builds rooms, grains, packet round-trips and
  databases without a silo; `docs/patterns/UnitTestPattern.cs` shows the shape.

### Test helpers at a glance
Read a `Turbo.Tests/Support` file only when you need a member not listed here.
- `RoomHarness(width, height, heights?)`: a room grain with real modules and no silo; `.Room`,
  `.Module<T>()`, `.CreateFloorItem(...)`, `.AddToRoom(item)`, `.TileHeight(x, y)`, `.ItemsById`,
  `.Fakes`. `LiveRoomHarness`: the same built by the grain's real constructor (every system, event
  listener and logic registered); use it when events or logic assignment matter.
- `GrainHarness.Create(assembly, typeName, fakes, db?, playerId?)`: any other grain with its live
  state, the given database, default configs and a recording grain factory.
- `SessionHarness`: the real session gateway with the real presence grain; `.NewSession(n)`,
  `.Received(key)`, `.WasClosed(key)`. `SessionAsserts.PresenceObserver(...)` finds the observer the
  presence routes to.
- `PacketHarness`: client bytes -> revision parser -> handler -> serializer; `Incoming(name)` /
  `Outgoing(name)` header ids, `Payload(w => ...)`, `await SendAsync(header, payload)` returns the
  replies as readable packets.
- `SettingsHarness(db, appSettingsJson, (services, config) => services.Configure<T>(...))`: the real
  server settings (an appsettings.json, the panel's overrides, the environment under `.EnvironmentPrefix`);
  `.Settings`, `.Running<T>()` (what the server took at start), `.Restart()`. `TestHotelConfig` has every kind.
- `InMemoryDb(throwOnUnorderedTake)` and `SqliteDb` (relational; `.Insert(entity)`) are database
  factories. `CapturingLogger<T>.AtLeast(level)` asserts on log output.
- `Fakes`: an unconfigured call returns a completed task, `default`, or another fake for an
  interface, so a grain method returning a record or class gives null. Stub it with
  `Fakes.Handlers["MethodName"] = call => ...` or `Fakes.Instances[(typeof(IMyGrain), key)] = ...`;
  `Fakes.Log` records the calls made.
- Copy the shape of the one example test closest to your subject; there is no need to read them all.

### Verification loop (once each, in this order)
1. `dotnet test Turbo.Tests/Turbo.Tests.csproj --filter <YourTestClass>` until it passes.
2. Show it catches the bug: `git stash push -- <the production files you changed>`, run step 1 again
   and expect it to fail, then `git stash pop`.
3. At the end, one `TurboCloudQualityGate` build and one full `dotnet test`.

## Validation commands
```bash
dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudFastCheck
dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudQualityGate
dotnet test Turbo.Tests/Turbo.Tests.csproj
```
