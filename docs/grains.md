# Grains & Orleans

- [Introduction](#introduction)
- [Grains in Five Minutes](#grains-in-five-minutes)
- [Turns](#turns)
    - [Code That Runs in a Room's Turn](#code-that-runs-in-a-rooms-turn)
    - [Never Wait on Yourself](#never-wait-on-yourself)
- [Where Each Hotel Call May Be Made](#where-each-hotel-call-may-be-made)
- [Reaching a Grain Directly](#reaching-a-grain-directly)
- [Keeping State in a Plugin](#keeping-state-in-a-plugin)

## Introduction

Turbo runs on [Orleans](https://learn.microsoft.com/dotnet/orleans/): each player, room, wallet
and inventory is a **grain**, a small object with its own state that Orleans keeps in memory while
it is used. You can write a whole plugin without naming a grain, through the
[hotel API](hotel-api.md), but you can't write a correct one without knowing the one rule grains
impose: **a grain does one thing at a time**. This page explains that rule and what it means for
the code you write.

For how core configures and uses Orleans (storage, streams, the silo), see
[Orleans Internals](orleans.md).

## Grains in Five Minutes

- A grain is keyed by an id: room `42` is always the same grain, wherever it is called from.
- It is activated on demand, the first time it is called, and collected when it has been idle a
  while. You never create or destroy one.
- It handles **one call at a time**. A call waits until the grain is free; inside a call, the
  grain's state is yours alone, with no locks.
- Calls are asynchronous messages. Awaiting a grain from another grain is normal and cheap.

## Turns

A grain's handling of one call, from start to finish, is a **turn**. While a room is in a turn,
everything else that wants that room waits: other players' clicks, timers, wired.

### Code That Runs in a Room's Turn

Several kinds of plugin code are called by the room itself, inside its turn:

- [furni logic](furni-logic.md) hooks and their scheduled runs;
- [wired boxes](plugin-wired.md);
- [room event listeners](plugin-room-events.md);
- [chat commands](commands.md) typed in the room (not operator commands);
- room arrival and avatar effect [policies](plugin-policies.md).

This code reads the room's state directly and in memory: its users, its floor, its furni. Keep it
quick, because the room waits for it.

### Never Wait on Yourself

> [!WARNING]
> Code in a room's turn must never await a call back into the **same** room. The call waits for
> the turn to end, and the turn waits for the call: the room hangs until Orleans times the call out.

So a room listener must not await `hotel.Rooms.PlayersAsync(itsOwnRoom)`, and a command must not
await `hotel.Rooms.SendAsync(theRoomItWasTypedIn, ...)`. Use what the turn gives you instead
(`IRoomTurn`, `ICommandRoom`), which acts on the room in place. Calling **another** grain is fine:
a player's wallet, their inventory, another room.

The database is the other thing a turn must not wait on. Copy what you need out of the turn and
read the database after, from a handler, a web route or a task.

## Where Each Hotel Call May Be Made

Every member of the hotel API says where it may be called from with `[HotelCall]`
([`Turbo.Contracts/Hotel/IHotel.cs`](../Turbo.Contracts/Hotel/IHotel.cs)):

| `HotelCallContext` | Means | Examples |
| --- | --- | --- |
| `Anywhere` | In memory. Fine in any turn. | `Players.OnlineIds`, `Wallet.CurrencyExists`, `Clock` |
| `NotFromItsGrain` | Asks the grain of the player or room it is given. Never await it inside that grain's own turn. | sends, whispers, wallet changes, badges, alerts |
| `OffGrain` | Reads the database. Never inside a room's turn. | player and room reads, search, toplists |

Your editor shows the attribute on each member; [Contracts: The Hotel API](hotel-api.md) lists them
all.

## Reaching a Grain Directly

Plugin classes can take `IGrainFactory` by constructor, as core's own commands do, and core's
grain interfaces are in `Turbo.Primitives`:

```csharp
public sealed class PlayerNames(IGrainFactory grains)
{
    public Task<ImmutableDictionary<PlayerId, string>> OfAsync(List<PlayerId> ids, CancellationToken ct) =>
        grains.GetPlayerDirectoryGrain().GetPlayerNamesAsync(ids, ct);
}
```

Prefer `IHotel` when it has what you need: it keeps the same rules as the grains, says where each
call may be made, and has an in-memory twin, `FakeHotel`, for your tests. A grain interface has
neither, and changes with core.

## Keeping State in a Plugin

A plugin declares no grains of its own. Orleans registers grain types once, as the server starts,
and a plugin is loaded, reloaded and unloaded while the server runs. Keep a plugin's state:

- in memory, in a singleton service the plugin registers, for what can be rebuilt (a cache, a
  leaderboard read at start);
- in the plugin's own tables, for what must survive a restart ([Database](database.md#a-plugins-tables));
- in a furni's state or extra data, for what belongs to one furni ([Furni Logic](furni-logic.md)).

A singleton is shared by every call that reaches it at once (handlers run side by side), so guard
its state as you would any shared object.
