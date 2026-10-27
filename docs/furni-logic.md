# Furni Logic & Room Turns

- [Introduction](#introduction)
- [Writing Furni Logic](#writing-furni-logic)
    - [Generating Logic](#generating-logic)
    - [Making It Placeable](#making-it-placeable)
- [Hooks](#hooks)
    - [Who May Use It](#who-may-use-it)
- [The Furni Context](#the-furni-context)
    - [Walking a User to the Furni](#walking-a-user-to-the-furni)
    - [Timers](#timers)
    - [The Furni's Own Data](#the-furnis-own-data)
    - [Firing Wired](#firing-wired)
- [The Room Turn](#the-room-turn)
- [Linked Pairs](#linked-pairs)
- [Messages From Your Own Code](#messages-from-your-own-code)
- [Sending a Player Elsewhere](#sending-a-player-elsewhere)

## Introduction

Every floor furni in a room has **logic**: what it does when someone double-clicks it, steps on
it or steps off it. Core's furniture definitions name the logic each one runs (`default_floor`,
a dice, a teleporter...). A plugin adds logic of its own with a class, and any furniture definition
whose logic is the class's key runs it: a fortune machine, a portal, a vending machine with its own
rules.

Furni logic is written against `Turbo.Contracts` alone. It never sees a grain, a room module or a
packet: it gets an `IFurniContext`, which is the room in its turn plus the furni itself, and acts
through it.

## Writing Furni Logic

A furni logic is a public class deriving from `FurniLogic`, marked with `[FurniLogic("<key>")]`:

```csharp
using Turbo.Contracts.Furni;
using Turbo.Contracts.Rooms;

[FurniLogic("fortune_machine")]
public sealed class FortuneMachineLogic : FurniLogic
{
    private static readonly string[] Fortunes = ["Today is your day.", "Beware of rollers.", "A rare is coming."];

    public override async Task<bool> OnUseAsync(IFurniContext ctx, RoomUser? user, int param, CancellationToken ct)
    {
        if (user is null)
            return false;

        await ctx.SetStateAsync(1, ct);

        ctx.Schedule(TimeSpan.FromSeconds(2), async ct =>
        {
            await ctx.SayAsync(user, Fortunes[Random.Shared.Next(Fortunes.Length)], RoomSpeech.Whisper, ct);
            await ctx.SetStateAsync(0, ct);
        });

        return true; // handled: the furni does not cycle its states as it would by default
    }
}
```

There is nothing to register. The server finds the class as the plugin loads, gives every furni
whose definition's logic is `fortune_machine` to it, and gives them back to their old logic when the
plugin unloads. A key core already has is replaced while the plugin is loaded, and the room
inspector says so. One instance is made per furni, by the plugin's container, so the constructor
can take the plugin's services and `IHotel`.

### Generating Logic

In a plugin made from the template:

```shell
npm run make logic fortune_machine
```

writes `server/Logic/FortuneMachineLogic.cs` with each hook ready to fill in.

### Making It Placeable

The logic runs for furniture whose definition names its key. Staff pick the key on the admin
panel's furniture page, whose logic list holds every key the server has registered, plugins'
included. Its furnidata gives it its sprite, name and description ([Gamedata](gamedata.md)).

## Hooks

Override the ones you need; each runs **after** core's own handling (a walk-on still fires core's
walk-on wired triggers):

| Hook | Runs when |
| --- | --- |
| `OnAttachedAsync(ctx, ct)` | the furni is placed, its room loads, or the plugin reloads |
| `OnDetachedAsync(ctx, ct)` | the furni is picked up, or its room unloads |
| `OnUseAsync(ctx, user, param, ct)` | a user double-clicks it; return `true` when you handled it |
| `OnWalkOnAsync(ctx, user, ct)` | a user steps onto it |
| `OnWalkOffAsync(ctx, user, ct)` | a user steps off it |
| `OnMessageAsync(ctx, message, sender, ct)` | your own code sent it a message ([below](#messages-from-your-own-code)) |

### Who May Use It

Two properties override the furniture definition:

```csharp
public override FurnitureUsageType? UsagePolicy => FurnitureUsageType.Everybody; // any visitor, not just rights holders
public override bool? Walkable => true;                                         // a tile the logic acts on as it is stepped on
```

Everything else (stacking, sitting, rolling, its states) is the definition's.

## The Furni Context

`IFurniContext` is the furni and its room. `ctx.Self` is the furni as it is now: its id, position,
rotation, state and definition (custom params included).

| Member | What it does |
| --- | --- |
| `SetStateAsync(state)` | sets the furni's state and shows it to the room |
| `ObjectExtra`, `SetObjectExtraAsync(n)` | the number the client gets with the furni; kept while the room is loaded, so set it in `OnAttachedAsync` if it must last |
| `ReadExtraData<T>(section)`, `ReadExtraDataJson(section)` | one section of the furni's extra data (its own, else its definition's) |
| `TileInFront`, `TilesBeside(user)`, `StandsBeside(user)` | the tile it faces; the tiles touching it a user may stand on, nearest first; whether the user stands beside it |
| `WalkToAsync`, `WalkToInOrderAsync` | walk a user to it ([below](#walking-a-user-to-the-furni)) |
| `Schedule(delay, run)`, `CancelSchedule()` | a timer ([below](#timers)) |
| `FireWiredAsync(evt, user)` | fire this room's wired triggers ([below](#firing-wired)) |
| `ShowStepAsync(user, tile)` | show one step towards a tile without moving the user (a step through a wall) |
| `ForwardToRoomAsync`, `WatchRoomAsync` | send a player to, or let them watch, another room ([below](#sending-a-player-elsewhere)) |
| `PairedItemId`, `GetRoomOfItemAsync(itemId)` | the other half of a linked pair, and the room an item stands in |

### Walking a User to the Furni

A machine used from in front of it walks the user there first, the way core's own walk-to-use
furni do:

```csharp
public override async Task<bool> OnUseAsync(IFurniContext ctx, RoomUser? user, int param, CancellationToken ct)
{
    if (user is null)
        return false;

    IReadOnlyList<RoomTilePoint> tiles = ctx.TileInFront is { } front ? [front, .. ctx.TilesBeside(user)] : ctx.TilesBeside(user);

    return await ctx.WalkToInOrderAsync(user, tiles, async (arrived, ct) => await DispenseAsync(ctx, arrived, ct), ct);
}
```

`onArrived` runs in the room's turn once the user stands there, at once if they already do. It
never runs if the user walks elsewhere or takes too long. `WalkToAsync` tries the nearest tile
first; `WalkToInOrderAsync` tries them in the order given. Both answer `false` when no tile can be
reached.

### Timers

`ctx.Schedule(delay, run)` runs `run` once, `delay` later by the room's clock, in the room's turn: a
spin's result, a rest's end, a game's next second. A furni has one timer: a later call replaces the
one waiting, and `CancelSchedule()` forgets it. It never runs once the furni is picked up or its room
unloads, so there is nothing to clean up.

### The Furni's Own Data

A furni's extra data is JSON in sections. `ReadExtraData<T>("my_section")` reads one section as a
`T` (the furni's own, else its definition's), `null` when there is none. Use the definition's for
settings staff give a whole kind of furni, and `Self.State` or `ObjectExtra` for what changes as it
is used.

### Firing Wired

```csharp
public sealed record ButtonPressed : IWiredEvent;

await ctx.FireWiredAsync(new ButtonPressed(), user, ct);
```

The room's wired triggers that listen for `ButtonPressed` (your plugin's own
[triggers](plugin-wired.md)) hear it on the room's next wired tick. From outside a room, use
`IHotel.Wired.FireAsync`.

## The Room Turn

The context is also an `IRoomTurn`: the room as it is in its own turn, which is what any code the
room runs gets. Everything on it is in memory:

| Member | What it does |
| --- | --- |
| `RoomUsers` | everyone in the room: players, pets and bots, where they stand and face, walking, idle, their effect |
| `Map` | the floor: `IsFloor(tile)`, `IsFree(tile)`, `CanStand(user, tile)`, `FurniAt(tile)` |
| `SayAsync(user, text, speech)` | a bubble over a player or bot (`RoomSpeech.Talk`, `RoomSpeech.Whisper`, or your own style and width); filtered, but not chat |
| `SetEffectAsync(user, effectId)` | puts an effect on a user; 0 takes it off |
| `StopWalkingAsync(user)`, `SetRotationAsync(user, rotation)` | stop a user; turn them |
| `SetBotFigureAsync(bot, figure)`, `SetBotFollowingAsync(bot, target)` | dress a bot; have it follow a user |
| `PlaceTemporaryFurniAsync(definition, tile, rotation)`, `RemoveTemporaryFurniAsync(id)` | a furni that is never saved and goes when removed or the room unloads |

`RoomUser` is a read-only snapshot: every action is on the context, and each answers `false` when
the user has left the room.

> [!WARNING]
> Hooks run in the room's turn. Keep them quick, and never await the hotel API on this same room
> (`hotel.Rooms.SendAsync(ctx.Room, ...)`): the room would wait on itself. A player's wallet or
> inventory is another grain and is fine. See [Grains & Orleans](grains.md#never-wait-on-yourself).

## Linked Pairs

`[FurniLogic("portal", LinkedPair = true)]` makes the furni a linked pair while the plugin is loaded,
exactly as a teleporter is: one bought from the catalogue is two, each naming the other
(`ctx.PairedItemId`), and it cannot be sent as a gift. A player one half sends
(`ctx.ForwardToRoomAsync`) to the room its other half stands in passes that room's door (doorbell,
password) as a teleporter's does; bans, a full room and the entry policies still decide. Core
checks the pair; your logic cannot claim it.

## Messages From Your Own Code

A packet handler, command or task reaches its furni logic through the room:

```csharp
var reply = await hotel.Rooms.InvokeFurniLogicAsync(
    roomId,
    itemId,
    FurniLogicMessage.Of("portal", new SetTarget(targetRoom), sender: playerId),
    ct);
```

The logic answers in the room's turn:

```csharp
public override async Task<FurniLogicReply> OnMessageAsync(
    IFurniContext ctx, FurniLogicMessage message, RoomUser? sender, CancellationToken ct)
{
    if (message.Read<SetTarget>() is not { } target)
        return FurniLogicReply.NotHandled;

    await ctx.SetObjectExtraAsync(target.RoomId, ct);

    return FurniLogicReply.Handled();
}
```

The payload is a string (`Of` writes JSON, `Read<T>` reads it back): your own types never cross
grains, because your plugin can be reloaded. The caller never gets an exception; the reply's
`Status` is `Handled` (with its `Payload`), `NotHandled`, `UnknownItem`, `WrongLogic` (the item runs
another logic) or `Failed` (the logic threw, and the room logged it).

## Sending a Player Elsewhere

- `ctx.ForwardToRoomAsync(user, room, extension)` sends a player to another room as if they walked
  in: bans, the door, a full room and the entry policies decide. `extension`
  (`RoomEntryExtension(key, payload)`) is your data for that room's arrival policies, which may
  place them somewhere other than the door ([Policies & Events](plugin-policies.md)).
- `ctx.WatchRoomAsync(user, room, envelope, lease)` lets a player watch another room from this one,
  its packets inside your envelope message ([Room Holds & Watching](plugin-room-watch.md)).
