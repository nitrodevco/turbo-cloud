---
name: turbo-room-holds-watches
description: Keep a Turbo room loaded and awake with no one in it (IHotel.Rooms.HoldAsync/ReleaseAsync) and send a player another room's view and live packets inside the plugin's envelope message (WatchAsync/UnwatchAsync, IFurniContext.WatchRoomAsync), with their leases, limits and answers. Use for portals, screens showing another room, or anything drawing a room the player is not in.
---

# Room holds and watches

## When to use this skill

A portal showing the room it leads to, a stage screen, a lobby sign that needs its room ticking.
Core keeps rooms as Habbo does (an empty room goes dormant and unloads 35 s after the last player
left; a room's packets reach only players in it); holds and watches are the two ways around that.
Both are the plugin's, lease-bound, and end on their own when the plugin unloads.

## Hold a room

<!-- compiled as the plugin "demo" -->

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Hotel;
using Turbo.Contracts.Plugins.Scheduling;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Watching;

namespace Demo;

/// <summary>Keeps the stage room awake: the lease is renewed every minute, before it runs out.</summary>
[Every("00:01:00")]
public sealed class StageHold(IHotel hotel) : IScheduledTask
{
    public static readonly RoomId STAGE = new(42);

    public async Task RunAsync(ScheduledTaskRun run, CancellationToken ct)
    {
        var held = await hotel.Rooms.HoldAsync(STAGE, "stage", TimeSpan.FromMinutes(2), ct);

        if (held != RoomHoldOutcome.Held)
            throw new InvalidOperationException($"The stage could not be held: {held}.");
    }
}
```

- `HoldAsync(room, holder, lease, ct)` loads the room and keeps it ticking as if someone were in it;
  the same holder renews. Lease 1 s to 10 min. `ReleaseAsync(room, holder, ct)` ends it.
- Answers: `Held`, `RoomFull` (256 holds a room), `PluginFull` (100 rooms a plugin per server), `Failed`.
- A held room costs what an occupied room costs: hold only while needed.

## Watch a room

The player gets the far room as an entry draws it (floor, furni, avatars, statuses), then every
packet the room sends its players, each inside your envelope's last field, in order.

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Hotel;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Watching;

namespace Demo;

/// <summary>The envelope: the watch fills Carried with each of the room's packets.</summary>
[PluginMessage("demo.far_room")]
public sealed record FarRoomMessage(int RoomId, IComposer? Carried = null) : IPluginMessage;

public sealed class Spectators(IHotel hotel)
{
    public async Task<bool> WatchAsync(PlayerId player, RoomId room, CancellationToken ct) =>
        await hotel.Rooms.WatchAsync(player, room, new FarRoomMessage(room.Value), TimeSpan.FromMinutes(1), ct)
            == RoomWatchOutcome.Watching;
}
```

- Renew by calling `WatchAsync` again before the lease runs out (a renewal sends no new view).
- It ends on its lease, `UnwatchAsync`, the player's session ending, the player walking into that
  room (from then on they get its packets normally), or the plugin unloading.
- A player watches only a room they could walk into (`Banned`, `DoorClosed`); a full room does not
  matter. At most 4 watched rooms a player, each for one plugin (`TooManyWatches`,
  `WatchedForAnotherPlugin`); `NotOnline`, `InThatRoom`.
- From furni logic, in the room's turn: `ctx.WatchRoomAsync(user, farRoom, envelope, lease, ct)` (asked,
  not awaited). A linked pair's half watching its other half's room passes that room's door.

## On the client

```ts
const portal = api.room.createSecondary(roomB, { offset: { x: 12, y: 0, z: 0 }, greyscale: 1, alpha: 0.75 });

api.messages.on('demo.far_room', ({ roomId, carried }) => {
    if (roomId === roomB) portal?.apply(carried);
});
```

See the turbo-client-modules skill ("Secondary rooms").

## Test

`FakeHotel` keeps the rules: `hotel.Rooms.HoldAsync`/`WatchAsync`, limits, `FakeRoom.Banned` and
`DoorClosed`, leases by `hotel.Clock`; read back with `hotel.Rooms.IsHeld(room)`,
`Watching(player)`, `WatchEnvelope(player, room)`. Guide: `plugin-room-watch.md`.
