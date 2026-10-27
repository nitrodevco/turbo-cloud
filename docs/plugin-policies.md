# Policies & Events

- [Introduction](#introduction)
- [Policies](#policies)
    - [Avatar Effects](#avatar-effects)
    - [Handing a Purchase Over](#handing-a-purchase-over)
    - [Order and Conflicts](#order-and-conflicts)
    - [Where They Run](#where-they-run)
    - [HC Members Pay 50% for Some Furni](#hc-members-pay-50-for-some-furni)
    - [Closing a Room for an Event](#closing-a-room-for-an-event)
- [Post-Commit Events](#post-commit-events)
    - [Addresses](#addresses)
    - [A Room Entry Ad](#a-room-entry-ad)

## Introduction

Most of what a hotel wants to change about Habbo is not a new feature but a rule on an old one:
club members pay half, nobody enters the event room before eight, the tenth account from one
address is refused. **Policies** are those rules. A policy is a small class the hotel asks before it
does something, and it allows the action, refuses it with a reason the player is shown, or changes
it:

```csharp
public sealed class ClubDiscountPolicy(IHotel hotel) : IPurchasePolicy
{
    public async ValueTask<PurchaseDecision> DecideAsync(
        PurchaseRequest request, PurchaseDecision proposed, CancellationToken ct) =>
        (await hotel.Players.ClubAsync(request.PlayerId, ct)).Active
            ? proposed with { CostCredits = proposed.CostCredits / 2 }
            : proposed;
}
```

What happened afterwards is an **event**: a handler hears it once the action is saved, and can
react (a reward, a log, a webhook) but never undo it.

Two of the extension kinds in `docs/extensions.md` (§3) let a plugin take part in the hotel's own
actions:

- A **policy** is asked *before* an action is done, and may allow it, refuse it with a reason the
  player is shown, or change it (a purchase's price, a room to go to instead).
- A **post-commit event** is told *after* an action is done and saved. It cannot undo or veto it,
  and it runs off the turn of whatever did it, so it may take its time.

Neither needs registering. A public class implementing a policy interface, or an
`IEventHandler<T>` for one of the events, anywhere in the plugin's assembly is found as the plugin
loads (built once with the plugin's services and the hotel's, `IHotel` included) and taken away as
it unloads. `npm run make policy purchase|room-entry|placement|registration|avatar-effect|purchase-fulfilment|public-room-order|room-arrival [<name>]` in a plugin made from
`templates/turbo-plugin` writes one.

With no plugin policy the hotel decides as it always has: the official checks (the offer, the club
level, the balance; bans, passwords, a full room, the doorbell; building rights, the spot, the
room's limits) are the hotel's own and are not policies.

## Policies

| Interface (`Turbo.Primitives.Policies`) | Asked | Answers |
| --- | --- | --- |
| `IPurchasePolicy` | before a catalogue offer is paid for, `request.Kind` saying which way: bought for oneself (`Catalog`: the catalogue, the builders club catalogue; `ClubMembership`: an offer giving a membership, the club centre's extend buttons included), as a gift (`Gift`, `request.GiftReceiverId`), as a room ad (`RoomAd`, `request.RoomAd`: the room, category, name, description, whether it extends the running one, how long; an ad is one ad, so `Units` is not read), a club gift a member picks (`ClubGiftClaim`) or a player entering a limited edition's sale (`LimitedEdition`); for the last two only a refusal is read. `request.ExtraParam` is the offer's extra parameter as the client sent it | `proposed` to let it stand, `proposed with { CostCredits = ..., CostSilver = ..., CostActivityPoints = ..., Units = ... }` to change what it costs in all or how many it gives (a gift always holds one), `PurchaseDecision.Refuse("why")`, or `PurchaseDecision.Refuse(error: CatalogPurchaseErrorType.RequiresHabboClub)` to show one of the hotel's own purchase errors |
| `IRoomEntryPolicy` | before a player enters a room the hotel lets them into, or rings at its doorbell (`request.Doorbell`) | `RoomEntryDecision.Allow`, `.Refuse("why")`, `.Redirect(roomId, "why"?)` |
| `IRegistrationPolicy` | before a new account is made, once the hotel's own checks passed (the name's rules and filter, a free name, the motto's and figure's lengths): the name, motto and figure as they would be saved (the gender's default figure when none was chosen), `request.Source` (`Site`, `Panel`, `Hotel`) and `request.RemoteAddress` (the site visitor's; empty from the panel) | `RegistrationDecision.Allow`, `.Refuse("why")` |
| `IPlacementPolicy` | before a player places a floor or wall item (from their inventory, the builders club or a sticky note pad) or saves the room's floor plan (`PlacementKind.FloorPlan`, the plan in `request.ModelData`) | `PlacementDecision.Allow`, `.Refuse("why")` |
| `IAvatarEffectPolicy` | `Wear`: when the hotel puts an effect on an avatar in a room (the player's own, an effect tile, wired, a game team, riding, a freeze; the avatar's kind and player); `Show`: for each player in the room it is sent to, the arriving player included (`view.ViewerId`). Taking an effect off is never asked | the effect (`proposed`/`shown` to let it stand), another, or 0 for none |
| `IPurchaseFulfilment` | `Fulfils`: after the purchase policies, for an offer bought for oneself or a room ad (never a gift); `FulfilAsync`: once its price is taken | `Fulfils` true to hand it over in the hotel's place; `FulfilAsync` throws to refund it |
| `IRoomArrivalPolicy` | as the room makes the avatar of a player it has let in (every entry check, `IRoomEntryPolicy` included, has passed): `request.Method`, `request.SourceRoomId` and `request.Extension`, what a plugin's furni sent with the player (`IFurniContext.ForwardToRoomAsync`, `docs/plugin-wired.md`). Not asked for a player arriving inside a teleporter | `proposed` (the door), `proposed.StartAt(x, y, rotation)` to appear there instead of on the door, `proposed with { WalkTo = new(x, y) }` to walk on from where they appear. It never decides whether they come in; a start tile they cannot stand on (off the map, closed, a furni nobody stands on; other players are no obstacle) means the door |
| `IPublicRoomOrder` | each time a navigator Public tab block is built (`category`: its id, name, kind, plugin key; `rooms`: in the order so far, the order staff set at the start, `Population` the players in each now). Not Habbo's: Habbo lists them as staff set, as the hotel does with none | the same rooms in the order to list them; a room it leaves out follows in its place before, one it adds is not listed |

How the player hears of a refusal:

- **Purchase**: the reason in a pop-up (`NotificationDialog`, type `turbo.purchase.refused`, its
  `message` the reason); without a reason, the hotel's purchase error the refusal named
  (`PurchaseFailed`, the "you may not buy this" alert, by default). Nothing is charged or given, a
  club gift stays unclaimed and a limited edition's entry is not taken.
- **Room entry**: the reason in a pop-up (`turbo.room_entry.refused`) before the room's own "no
  entry"; a redirect closes this entry (the reason first, when given) and forwards the player to the
  other room, which they enter as any other, its policies asked again. A policy must not send a
  player back to the room they came from.
- **Registration**: the site's sign-up shows the reason under the name, the panel its error; no
  player is made. A cap on accounts per address keeps its own count from `PlayerRegisteredEvent`'s
  address (core keeps no registration address) and lets `Source == Panel` through.
- **Placement**: the reason in a pop-up (`turbo.placement.refused`), or the hotel's "you cannot place
  this item here" without one; the furni stays in the inventory. A refused floor plan is answered with
  the editor's own error pop-up, its errors the reason (and with nothing without one, as a save the
  room will not take is answered).

A hotel styles each pop-up type with `notification.<type>` in its external variables, as any
notification type. Moving or picking up furni already placed is not asked.

### Avatar Effects

Both methods run on the room's turn, `Show` once per player in the room for each change, so they
answer at once from what the plugin keeps in memory, never from a table or another grain. `Wear`
changes what the avatar has on (wired conditions read it); `Show` only what one client is sent:
players shown another effect are left out of the room's packet and sent their own, and a policy
without a `Show` of its own keeps the room's single packet for everyone. Answering 0 is safe either
way. Replacing an effect with another one leaves taking it off to the plugin: the hotel takes off
what it put on (leaving the effect tile, getting off the horse), never the replacement.

```csharp
/// <summary>Players who turned effects off see nobody's, their own included.</summary>
public sealed class EffectsOffPolicy(EffectsOff off) : IAvatarEffectPolicy
{
    public int Show(AvatarEffectView view, int shown) => off.Has(view.ViewerId) ? 0 : shown;
}
```

`EffectsOff` is the plugin's own singleton (a set loaded from its table at login, changed by its
command). What a player was already sent stays until the next change or room entry; the command
re-sends what is worn (an `AvatarEffect` composer per avatar through `hotel.Players.SendAsync`) to
show it at once.

### Handing a Purchase Over

A fulfilment replaces what the hotel hands over, not the purchase: the offer, the club level, the
safety lock, the owner's right to the room and the purchase policies are asked as always, and the
price (as the policies left it) is taken once, before `FulfilAsync`. When it throws, the price goes
back and the client gets its purchase error; a `CatalogPurchaseException` it throws is the one the
client is told. What only the hotel's own hand-over needs is not checked (a pet's name, a trophy's
inscription, for a room ad the room's running event) and not done (the furni, the badge, the
membership, the room event). `PurchaseCompletedEvent` is raised as for any purchase, its `RoomAd`
null. The client still gets `PurchaseOK`; what else the buyer sees is the plugin's.

```csharp
/// <summary>A room ad is the hotel's own promotion bar, not a room event.</summary>
public sealed class PromotedRooms(PromotionBoard board) : IPurchaseFulfilment
{
    public bool Fulfils(PurchaseRequest request) => request.RoomAd is not null;

    public async ValueTask FulfilAsync(PurchaseRequest request, PurchaseDecision paid, CancellationToken ct) =>
        await board.AddAsync(request.RoomAd!.RoomId, request.RoomAd.Name, request.RoomAd.Duration, ct);
}
```

### Order and Conflicts

Policies of a kind run in `Order` (an `int Order => ...` on the class, 0 by default, lower first),
then plugin key, then as registered. A purchase's policies each take the decision of the ones before
(`proposed`), the offer's own price first, so they compose: a discount and a surcharge from two
plugins both apply, in that order. Avatar effect policies, public room orders and room arrivals compose the same way. For room entry and
placement the first refusal is the answer; of purchase fulfilments, the first that takes the
purchase hands it over.

Two plugins with a policy of the same kind both run; the server logs it as a conflict ("Conflict:
alpha, beta each have a purchase policy ...") and lists it on both in the admin API's plugin view
(`policies`, each with the other owners in `sharedWith`), since the later one decides on what the
earlier one left. Give one an explicit `Order` when the order matters.

A purchase policy that throws refuses the purchase (fail closed): it is logged, the buyer gets the
hotel's purchase error, nothing is charged and the policies after it are not asked. A policy of any
other kind that throws is logged and passed over (it allows). A cancelled one cancels the action.

### Where They Run

A purchase policy runs on the buyer's purchase turn: it may read anything (the hotel API, a grain, its
own tables) but must not buy for the same player. Room entry and placement policies run off the
room's turn and may ask the room anything. Keep them quick: the player waits on each. A room arrival
policy runs on the room's turn, as the avatar is made: answer from the request and from memory, and
never ask that room anything.

### HC Members Pay 50% for Some Furni

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

### Closing a Room for an Event

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

## Post-Commit Events

| Event | Raised |
| --- | --- |
| `PurchaseCompletedEvent` (`Turbo.Primitives.Catalog.Events`) | a catalogue offer was paid for and handed over (a limited edition when its sale or raffle win is charged), or a club gift claimed: buyer, `Kind` (as `PurchaseRequest.Kind`), offer and product code, units given, what was paid in each currency, `ItemIds` (the furni put in the buyer's inventory), the receiver of a gift; for a room ad, `RoomAd` (the room event as it now runs) and `RoomAdExtended` |
| `PlayerLoggedInEvent` (`Turbo.Primitives.Players.Events`) | a player's login ticket was taken and the connection is theirs: every login, one replacing an earlier connection included; `RemoteAddress` |
| `PlayerLoginFailedEvent` (`Turbo.Primitives.Players.Events`) | a login was turned away: `Failure` (`InvalidTicket`, `Banned`, `Maintenance`, `RefusedByPolicy` with the policy's `Reason`), whose ticket it was (null for an unknown one), `RemoteAddress` |
| `PlayerRegisteredEvent` (`Turbo.Primitives.Players.Events`) | a new player account was saved (the site's registration, the panel's): `Source`, `RemoteAddress` |
| `PlayerRegistrationFailedEvent` (`Turbo.Primitives.Players.Events`) | an account was asked for and not made (the name's rules, a taken name, a registration policy): the name asked for, the reason given, `Source`, `RemoteAddress` |
| `PlayerIdentityLinkedEvent` (`Turbo.Primitives.Players.Events`) | an account elsewhere (Discord) was linked to a player (`IHotel.Players.LinkIdentityAsync`); `TakenFrom` when staff moved it from another player |
| `RoomEnteredEvent` (`Turbo.Primitives.Rooms.Events`) | a player entered a room: it was sent to them and their avatar is in it |
| `HotelAvailabilityChangedEvent` (`Turbo.Primitives.Availability.Events`) | a maintenance, shutdown or restart was scheduled or called off, or its countdown ran out: `Previous` and `Current` (what `IHotel.Status.Current` reads from now on) |

Each derives from `DomainEvent`: published once, after the change is saved, never for an action that
was refused or failed (the two `...FailedEvent`s are the refusal itself, raised once it was given), and on the thread pool, not awaited by what raised it. `EventId` is the
event's own id, for a handler that must act once (a reward, a call to another service) to remember;
`OccurredAtUtc` when it happened. A handler that throws is logged and the others still run.
`IEventHandler<T>`, `IEventBehavior<T>` (one that runs before the handlers, ordered by `[Order]`) and
`EventContext` are contracts in `Turbo.Primitives.dll`, which every plugin references already, under
the namespaces they always had (`Turbo.Events.Registry`, `Turbo.Pipeline.Attributes`); a plugin
needs no reference to `Turbo.Events.dll` or `Turbo.Pipeline.dll` for them. A plugin publishes its
own events (a plugin notice, `docs/plugin-notices.md`) through `IEventPublisher`
(`Turbo.Primitives.Events`), taken from the host services.

### Addresses

`RemoteAddress` is where the client or site visitor connected from: the connection's own address,
or behind a proxy the hotel trusts the nearest one its `X-Forwarded-For` names. The site's API
believes a proxy on the same machine; the game server believes loopback and the addresses or
ranges in `Turbo:Networking:TrustedProxies` (`["10.0.0.0/8"]` for a WebSocket proxy on another
machine). A header from anyone else is ignored. The site's Play keeps the address beside the ticket
it issues (`security_tickets.ip_address`) until the login uses it up. An address is a player's
personal data: core never logs it, the events' text (`ToString()`) leaves it out, and a plugin that
keeps one (alts by address, an address ban list) keeps it in its own table and only as long as it
needs it. The login's play policies get it too (`PlayRequest.RemoteAddress`, `PlaySource.Login`;
`docs/plugin-web-routes.md`).

These are not the room's own events (`PlayerEnterEvent` and the others in `docs/plugin-room-events.md`),
which room listeners hear on the room's turn and must handle at once; `RoomEnteredEvent` is the slow
work's version of an entry.

### A Room Entry Ad

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
