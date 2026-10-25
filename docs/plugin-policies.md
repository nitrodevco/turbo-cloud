# Policies and post-commit events

Two of the extension kinds in `docs/extensions.md` (§3) let a plugin take part in the hotel's own
actions:

- A **policy** is asked *before* an action is done, and may allow it, refuse it with a reason the
  player is shown, or change it (a purchase's price, a room to go to instead).
- A **post-commit event** is told *after* an action is done and saved. It cannot undo or veto it,
  and it runs off the turn of whatever did it, so it may take its time.

Neither needs registering. A public class implementing a policy interface, or an
`IEventHandler<T>` for one of the events, anywhere in the plugin's assembly is found as the plugin
loads (built once with the plugin's services and the hotel's, `IHotel` included) and taken away as
it unloads. `npm run make policy purchase|room-entry|placement [<name>]` in a plugin made from
`templates/turbo-plugin` writes one.

With no plugin policy the hotel decides as it always has: the official checks (the offer, the club
level, the balance; bans, passwords, a full room, the doorbell; building rights, the spot, the
room's limits) are the hotel's own and are not policies.

## Policies

| Interface (`Turbo.Primitives.Policies`) | Asked | Answers |
| --- | --- | --- |
| `IPurchasePolicy` | before a catalogue offer is paid for, bought for oneself (the catalogue, the builders club catalogue) or as a gift | `proposed` to let it stand, `proposed with { CostCredits = ..., CostSilver = ..., CostActivityPoints = ..., Units = ... }` to change what it costs in all or how many it gives (a gift always holds one), `PurchaseDecision.Refuse("why")` |
| `IRoomEntryPolicy` | before a player enters a room the hotel lets them into, or rings at its doorbell (`request.Doorbell`) | `RoomEntryDecision.Allow`, `.Refuse("why")`, `.Redirect(roomId, "why"?)` |
| `IPlacementPolicy` | before a player places a floor or wall item (from their inventory, the builders club or a sticky note pad) or saves the room's floor plan (`PlacementKind.FloorPlan`, the plan in `request.ModelData`) | `PlacementDecision.Allow`, `.Refuse("why")` |

How the player hears of a refusal:

- **Purchase**: the reason in a pop-up (`NotificationDialog`, type `turbo.purchase.refused`, its
  `message` the reason); without a reason, the hotel's own "you may not buy this" alert. Nothing is
  charged or given.
- **Room entry**: the reason in a pop-up (`turbo.room_entry.refused`) before the room's own "no
  entry"; a redirect closes this entry (the reason first, when given) and forwards the player to the
  other room, which they enter as any other, its policies asked again. A policy must not send a
  player back to the room they came from.
- **Placement**: the reason in a pop-up (`turbo.placement.refused`), or the hotel's "you cannot place
  this item here" without one; the furni stays in the inventory. A refused floor plan is answered with
  the editor's own error pop-up, its errors the reason (and with nothing without one, as a save the
  room will not take is answered).

A hotel styles each pop-up type with `notification.<type>` in its external variables, as any
notification type. Moving or picking up furni already placed is not asked.

### Order and conflicts

Policies of a kind run in `Order` (an `int Order => ...` on the class, 0 by default, lower first),
then plugin key, then as registered. A purchase's policies each take the decision of the ones before
(`proposed`), the offer's own price first, so they compose: a discount and a surcharge from two
plugins both apply, in that order. For room entry and placement the first refusal is the answer.

Two plugins with a policy of the same kind both run; the server logs it as a conflict ("Conflict:
alpha, beta each have a purchase policy ...") and lists it on both in the admin API's plugin view
(`policies`, each with the other owners in `sharedWith`), since the later one decides on what the
earlier one left. Give one an explicit `Order` when the order matters.

A policy that throws is logged and passed over (it allows, and a purchase keeps the decision so far);
a cancelled one cancels the action.

### Where they run

A purchase policy runs on the buyer's purchase turn: it may read anything (the hotel API, a grain, its
own tables) but must not buy for the same player. Room entry and placement policies run off the
room's turn and may ask the room anything. Keep them quick: the player waits on each.

### HC members pay 50% for some furni

```csharp
using Turbo.Contracts.Hotel;
using Turbo.Primitives.Policies;

/// <summary>Club members pay half the credits for the offers on the plugin's list.</summary>
public sealed class ClubDiscountPolicy(IHotel hotel, IOptionsMonitor<ShopSettings> settings)
    : IPurchasePolicy
{
    public async ValueTask<PurchaseDecision> DecideAsync(
        PurchaseRequest request,
        PurchaseDecision proposed,
        CancellationToken ct
    )
    {
        if (!settings.CurrentValue.ClubHalfPrice.Contains(request.Offer.LocalizationId))
            return proposed;

        return (await hotel.Players.ClubAsync(request.PlayerId, ct)).Active
            ? proposed with { CostCredits = proposed.CostCredits / 2 }
            : proposed;
    }
}
```

`ShopSettings` is the plugin's `[PluginSettings]` class, so staff edit the list on the panel. The
catalogue still shows the full price; the purchase charges half, and `PurchaseCompletedEvent` says
what was paid.

### Closing a room for an event

```csharp
public sealed class EventRoomPolicy(IOptionsMonitor<EventSettings> settings) : IRoomEntryPolicy
{
    public ValueTask<RoomEntryDecision> DecideAsync(RoomEntryRequest request, CancellationToken ct)
    {
        var current = settings.CurrentValue;

        return ValueTask.FromResult(
            current.Closed && request.RoomId.Value == current.RoomId
                ? RoomEntryDecision.Redirect(current.LobbyRoomId, "The event starts at 20:00.")
                : RoomEntryDecision.Allow
        );
    }
}
```

## Post-commit events

| Event | Raised |
| --- | --- |
| `PurchaseCompletedEvent` (`Turbo.Primitives.Catalog.Events`) | a catalogue offer was paid for and handed over: buyer, offer and product code, units given, what was paid in each currency, the receiver of a gift |
| `PlayerLoggedInEvent` (`Turbo.Primitives.Players.Events`) | a player's login ticket was taken and the connection is theirs: every login, one replacing an earlier connection included |
| `PlayerRegisteredEvent` (`Turbo.Primitives.Players.Events`) | a new player account was saved (the site's registration, the panel's) |
| `RoomEnteredEvent` (`Turbo.Primitives.Rooms.Events`) | a player entered a room: it was sent to them and their avatar is in it |

Each derives from `DomainEvent`: published once, after the change is saved, never for an action that
was refused or failed, and on the thread pool, not awaited by what raised it. `EventId` is the
event's own id, for a handler that must act once (a reward, a call to another service) to remember;
`OccurredAtUtc` when it happened. A handler that throws is logged and the others still run.
`IEventHandler<T>` is in `Turbo.Events`: the plugin references `$(TurboBin)/Turbo.Events.dll` as it
does `Turbo.Primitives.dll` (as for plugin notices, `docs/plugin-notices.md`).

These are not the room's own events (`PlayerEnterEvent` and the others in `docs/plugin-room-events.md`),
which room listeners hear on the room's turn and must handle at once; `RoomEnteredEvent` is the slow
work's version of an entry.

### A room entry ad

```csharp
using Turbo.Contracts.Hotel;
using Turbo.Events.Registry;
using Turbo.Primitives.Rooms.Events;

/// <summary>Tells a player entering one of the listed rooms what is on there.</summary>
public sealed class RoomAd(IHotel hotel, IOptionsMonitor<AdSettings> settings)
    : IEventHandler<RoomEnteredEvent>
{
    public async ValueTask HandleAsync(RoomEnteredEvent entered, EventContext ctx, CancellationToken ct)
    {
        if (settings.CurrentValue.Ads.TryGetValue(entered.RoomId.Value, out var text))
            await hotel.Notify.NotificationAsync(
                entered.PlayerId,
                "turbo.room_ad",
                new Dictionary<string, string> { ["display"] = "BUBBLE", ["message"] = text },
                ct
            );
    }
}
```

The same shape logs logins (`PlayerLoggedInEvent`), welcomes new players (`PlayerRegisteredEvent`) or
counts purchases for a quest (`PurchaseCompletedEvent`). In tests, publish the event to the handler
yourself; `Turbo.Testing.FakeHotel` records what it sent.
