# Core Test Helpers

What `Turbo.Tests/Support` offers for a regression test in core, so a test can be written without
reading the helpers' source. The rule that a behaviour change comes with a test, and the
verification loop, are in [`AGENTS.md`](../../AGENTS.md) under *Tests*;
[`docs/patterns/UnitTestPattern.cs`](../patterns/README.md) shows the shape. Plugin tests use
[the test hotel](../plugin-testing.md) instead.

## Helpers at a glance

Read a `Turbo.Tests/Support` file only when you need a member not listed here.
- `RoomHarness(width, height, heights?, clock?)`: a room grain with real modules and no silo;
  `.Room`, `.Module<T>()`, `.CreateFloorItem(...)`, `.AddToRoom(item)`, `.TileHeight(x, y)`,
  `.ItemsById`, `.Fakes`, `.Clock` (a `ManualTimeProvider` from a fixed start: room time moves
  only on `.Clock.AdvanceMs(ms)`, never wait with `Task.Delay`) and `.NowMs()`. `LiveRoomHarness`:
  the same built by the grain's real constructor (every system, event listener and logic
  registered); use it when events or logic assignment matter. `RoomHarness.AddPlayer(id)` puts a
  fake `IRoomPlayer` in the room as object `id` whose `PlayerId` is `id`.
- `WiredRoom(width, height, ..., avatarContexts?)`: a `LiveRoomHarness` with users, furni and wired
  boxes; `.Enter(objectId, x, y, name?, rotation?, playerId?)` stands a user in it (player
  `100 + objectId` by default), `.ContextFor(avatar or objectId)` is that user's `ActionContext`.
  `avatarContexts: true` makes a user's context answer `RoomObject` with the real avatar (walk-ons,
  teleports, doors).
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
  `Fakes.Log` records the calls made. What was sent, in order (`SentComposers`):
  `.Sent<T>(route?)` by any route (`SendRoute.Session`, `.Player`, `.Room`, `.Direct`, `.Any`),
  `.SentToRoom<T>()` (each room publish unwrapped), `.SentToPlayer<T>(playerId)` (presence sends,
  batches unwrapped), `.RoomPublishes()` (whole `RoomOutboundSnapshot`s) and `.AvatarUpdates()`
  (each `UserUpdateMessageComposer`'s avatars). `TrySendComposerAsync` is not a send there; read
  the log for it.
- Copy the shape of the one example test closest to your subject; there is no need to read them all.
