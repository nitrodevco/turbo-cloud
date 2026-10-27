---
name: turbo-furni-logic
description: Write furniture behaviour in a Turbo plugin with FurniLogic and [FurniLogic("key")] - use, walk on/off, walk-to-use, timers, state, extra data, linked pairs, forwarding to another room and messages from the plugin's own handlers - and act in a room's turn through IRoomTurn (speech, effects, bots, temporary furni). Use when a furni must do something, or when code runs inside a room's turn.
---

# Furni logic and the room's turn

## When to use this skill

A furni that does something when used, stepped on or timed; anything that acts on a room's users
from inside the room. Wired boxes are the turbo-wired-boxes skill.

## A furni logic

`npm run make logic coffee_machine` writes the shape. The key is the furniture definition's logic
name: staff pick it on the admin panel's furniture page (the list holds every registered key).
Everything the logic does not override (stacking, sitting, rolling) is the definition's.

<!-- compiled as the plugin "demo" -->

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Furni;
using Turbo.Contracts.Rooms;
using Turbo.Contracts.Wired;
using Turbo.Primitives.Rooms.Enums;

namespace Demo;

/// <summary>Raised to this room's wired when a cup is poured (the turbo-wired-boxes skill).</summary>
public sealed record CupPoured(int Cups) : IWiredEvent;

/// <summary>
/// A coffee machine: a user who uses it walks in front of it, it brews for three seconds (state 1)
/// and hands them a cup (an effect), then rests (state 0).
/// </summary>
[FurniLogic("demo_coffee")]
public sealed class CoffeeMachineLogic : FurniLogic
{
    private const int CUP_EFFECT = 4;

    public override FurnitureUsageType? UsagePolicy => FurnitureUsageType.Everybody;

    public override async Task<bool> OnUseAsync(IFurniContext ctx, RoomUser? user, int param, CancellationToken ct)
    {
        if (user is null || ctx.Self.State != 0)
            return true; // handled: the furni does not cycle its states

        await ctx.WalkToInOrderAsync(
            user,
            ctx.TileInFront is { } front ? [front, .. ctx.TilesBeside(user)] : ctx.TilesBeside(user),
            (arrived, ct2) => BrewAsync(ctx, arrived, ct2),
            ct
        );

        return true;
    }

    private static async Task BrewAsync(IFurniContext ctx, RoomUser user, CancellationToken ct)
    {
        await ctx.SetStateAsync(1, ct);
        ctx.Schedule(
            TimeSpan.FromSeconds(3),
            async ct2 =>
            {
                await ctx.SetStateAsync(0, ct2);
                await ctx.SetEffectAsync(user, CUP_EFFECT, ct2);
                await ctx.SayAsync(user, "Here is your coffee.", RoomSpeech.Whisper, ct2);
                await ctx.FireWiredAsync(new CupPoured(1), user, ct2);
            }
        );
    }
}
```

## The hooks

| Override | When |
| --- | --- |
| `OnAttachedAsync(ctx, ct)` | placed, room loaded, plugin reloaded: set up `ObjectExtra`, a timer |
| `OnDetachedAsync(ctx, ct)` | picked up, room unloading |
| `OnUseAsync(ctx, user, param, ct)` | double-click; `true` = handled, `false` lets the furni cycle its states as by default |
| `OnWalkOnAsync(ctx, user, ct)`, `OnWalkOffAsync(ctx, user, ct)` | a user steps on or off it (core's walk-on wired triggers still fire) |
| `OnMessageAsync(ctx, message, sender, ct)` | a message from the plugin's own code (below) |
| `UsagePolicy`, `Walkable` | who may use it (`Everybody`, ...); a tile the logic acts on as it is stepped on |

Each hook runs after core's own and in the room's turn.

## IFurniContext (is an IRoomTurn)

- The furni: `Self` (`Id`, `X`, `Y`, `Z`, `State`, `Rotation`, `Definition`), `SetStateAsync`,
  `ObjectExtra`/`SetObjectExtraAsync` (the number the client gets; set it in `OnAttachedAsync` if it
  must last), `ReadExtraData<T>(section)`, `ReadExtraDataJson(section)`, `PairedItemId`.
- Walking: `WalkToAsync(user, tiles, onArrived, ct)` (nearest first), `WalkToInOrderAsync` (best
  first), `TileInFront`, `TilesBeside(user)`, `StandsBeside(user)`, `ShowStepAsync(user, tile, ct)`.
- Time: `Schedule(delay, run)` once, by the room's clock, in the room's turn; one per furni, a later
  call replaces it; never runs after pickup or unload. `CancelSchedule()`.
- Wired: `FireWiredAsync(evt, triggerer, ct)` to this room's plugin triggers.
- Other rooms: `ForwardToRoomAsync(user, room, new RoomEntryExtension(key, payload), ct)` (a plain
  entry, except a linked pair's half sending to its other half's room passes the door like a
  teleporter), `WatchRoomAsync(...)` (turbo-room-holds-watches skill).

`IRoomTurn` (any code in a room's turn): `RoomUsers`, `Map` (`IsFloor`, `IsFree`, `CanStand`,
`FurniAt`), `SayAsync(user, text, RoomSpeech, ct)` (`RoomSpeech.Talk`, `.Whisper`, `with { Shout = true,
StyleId = .. }`), `SetEffectAsync`, `StopWalkingAsync`, `SetRotationAsync`, `SetBotFigureAsync`,
`SetBotFollowingAsync`, `PlaceTemporaryFurniAsync(definition, tile, rotation, ct)` and
`RemoveTemporaryFurniAsync(id, ct)`. `RoomUser` is a read-only view; every action answers false once
the user has left.

## Rules

- In the turn: never await `IHotel` on this same room (it waits on itself), never read the database.
  A player's wallet or inventory is another grain and is fine (`hotel.Wallet.CreditAsync`).
- State lives on the furni (`State`, `ObjectExtra`, extra data) or in a plugin service keyed by
  item id; never in fields of the logic (one instance serves every furni of the key).
- Linked pairs: `[FurniLogic("portal", LinkedPair = true)]` sells it in two halves like a teleporter.

## Messages from your own code

A command, handler or task reaches a furni through its room; strings only cross grains:

```csharp
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Furni;
using Turbo.Contracts.Rooms;
using Turbo.Primitives.Furniture;

namespace Demo;

public sealed record SetLabel(string Text);

[FurniLogic("demo_sign")]
public sealed class SignLogic : FurniLogic
{
    public override async Task<FurniLogicReply> OnMessageAsync(
        IFurniContext ctx, FurniLogicMessage message, RoomUser? sender, CancellationToken ct)
    {
        if (message.Read<SetLabel>() is not { } label)
            return FurniLogicReply.NotHandled;

        await ctx.SetObjectExtraAsync(label.Text.Length, ct);

        return FurniLogicReply.Handled();
    }
}
```

The caller: `await hotel.Rooms.InvokeFurniLogicAsync(room, itemId, FurniLogicMessage.Of("demo_sign",
new SetLabel("Open"), player), ct)`; the reply's `Status` is `Handled`, `NotHandled`, `UnknownItem`,
`WrongLogic` or `Failed`, never an exception.

## Check

`npm run check`; guide `plugin-wired.md` ("Furni logic"); scaffold `npm run make logic <key>`.
