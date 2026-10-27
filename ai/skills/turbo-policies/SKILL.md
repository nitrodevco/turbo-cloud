---
name: turbo-policies
description: Let a Turbo plugin allow, refuse or change hotel actions before they commit (IPurchasePolicy, IRoomEntryPolicy, IRoomArrivalPolicy, IPlacementPolicy, IRegistrationPolicy, IAvatarEffectPolicy, IPurchaseFulfilment, IPublicRoomOrder, IPlayPolicy) and react after them with post-commit events (IEventHandler<PurchaseCompletedEvent>, RoomEnteredEvent, PlayerLoggedInEvent...). Use for rules on buying, entering rooms, where players appear, placing furni, sign-up, or reacting to those.
---

# Policies and post-commit events

## When to use this skill

"Only club members may enter", "rares cost half for HC", "new accounts can't buy X", "appear at the
portal, not the door", "log every purchase". Never guard these with a packet behaviour
(`IMessageBehavior<T>`): it sees the packet before the hotel resolved it, misses the other ways in,
and leaves the client waiting when it blocks.

Both kinds are found as the plugin loads (public classes, built with the plugin's and the hotel's
services, `IHotel` included) and go when it unloads. With none, the hotel decides as Habbo does.

## Before: policies (`Turbo.Primitives.Policies`)

| Interface | Answer |
| --- | --- |
| `IPurchasePolicy.DecideAsync(request, proposed, ct)` | `proposed`, `proposed with { CostCredits = .. }`, `PurchaseDecision.Refuse("why")` |
| `IRoomEntryPolicy.DecideAsync(request, ct)` | `RoomEntryDecision.Allow`, `.Refuse("why")`, `.Redirect(roomId, "why")` |
| `IRoomArrivalPolicy.DecideAsync(request, proposed, ct)` | `proposed` (the door), `proposed.StartAt(x, y, rotation)`, `proposed with { WalkTo = new(x, y) }` |
| `IPlacementPolicy.DecideAsync(request, ct)` | `PlacementDecision.Allow`, `.Refuse("why")` (floor plan saves too) |
| `IRegistrationPolicy.DecideAsync(request, ct)` | `RegistrationDecision.Allow`, `.Refuse("why")` |
| `IAvatarEffectPolicy.Wear(request, proposed)` / `Show(view, shown)` | the effect, another, or 0 (room's turn, from memory) |
| `IPurchaseFulfilment.Fulfils(request)` / `FulfilAsync(request, paid, ct)` | hand the purchase over in the hotel's place; throw to refund |
| `IPublicRoomOrder.OrderRooms(category, rooms)` | the navigator Public tab block's rooms in your order |

`npm run make policy <purchase|room-entry|placement|registration|avatar-effect|purchase-fulfilment|public-room-order|room-arrival> [<name>]`
writes each. The player sees the refusal reason in a pop-up.

<!-- compiled as the plugin "demo" -->

```csharp
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Contracts.Hotel;
using Turbo.Contracts.Plugins;
using Turbo.Primitives.Policies;

namespace Demo;

[PluginSettings("Demo")]
public sealed class DemoSettings
{
    /// <summary>The room only club members may enter (0: none).</summary>
    public int ClubRoomId { get; init; }
}

/// <summary>Club members only in the configured room.</summary>
public sealed class ClubRoomPolicy(IHotel hotel, IOptionsMonitor<DemoSettings> settings) : IRoomEntryPolicy
{
    public async ValueTask<RoomEntryDecision> DecideAsync(RoomEntryRequest request, CancellationToken ct)
    {
        if (request.RoomId.Value != settings.CurrentValue.ClubRoomId)
            return RoomEntryDecision.Allow;

        return (await hotel.Players.ClubAsync(request.PlayerId, ct)).Active
            ? RoomEntryDecision.Allow
            : RoomEntryDecision.Refuse("This room is for club members.");
    }
}

/// <summary>Club members pay half the credits for offers whose name starts with "demo_".</summary>
public sealed class ClubDiscountPolicy(IHotel hotel) : IPurchasePolicy
{
    public int Order => 10; // lower runs first; each policy gets the decision of the ones before

    public async ValueTask<PurchaseDecision> DecideAsync(
        PurchaseRequest request, PurchaseDecision proposed, CancellationToken ct)
    {
        if (!request.Offer.LocalizationId.StartsWith("demo_"))
            return proposed;

        return (await hotel.Players.ClubAsync(request.PlayerId, ct)).Active
            ? proposed with { CostCredits = proposed.CostCredits / 2 }
            : proposed;
    }
}

/// <summary>A player one of the plugin's portals sent (ForwardToRoomAsync) appears beside it.</summary>
public sealed class PortalArrivalPolicy : IRoomArrivalPolicy
{
    public ValueTask<RoomArrivalDecision> DecideAsync(
        RoomArrivalRequest request, RoomArrivalDecision proposed, CancellationToken ct) =>
        ValueTask.FromResult(
            request.Extension is { Key: "demo.portal" } extension && int.TryParse(extension.Payload, out var x)
                ? proposed.StartAt(x, 1, proposed.Rotation)
                : proposed
        );
}
```

## Where they run (the rules that bite)

- Purchase: on the buyer's purchase turn; may read anything, never buy for the same player. One
  that **throws refuses** the purchase (fail closed). `request.Kind`: `Catalog`, `ClubMembership`,
  `Gift`, `RoomAd`, `ClubGiftClaim`, `LimitedEdition`.
- Room entry and placement: off the room's turn, may ask the room. Keep them quick: the player waits.
  The first refusal wins; one that throws is logged and passed over.
- Room arrival and avatar effects: **on the room's turn**: answer from the request and memory, never
  ask that room anything. Arrival never decides whether they come in.
- Several plugins with a policy of one kind all run (`Order`, then plugin key) and the overlap is
  logged as a conflict: give `Order` when it matters.

## After: post-commit events

`IEventHandler<T>` (`Turbo.Events.Registry`) hears a `DomainEvent` once the change is saved, off
the turn, not awaited: slow work is fine. `EventId` is stable: remember it to act once.

```csharp
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Events.Registry;
using Turbo.Primitives.Catalog.Events;
using Turbo.Primitives.Events;

namespace Demo;

public sealed class PurchaseAudit(ILogger<PurchaseAudit> logger) : IEventHandler<PurchaseCompletedEvent>
{
    public ValueTask HandleAsync(PurchaseCompletedEvent e, EventContext ctx, CancellationToken ct)
    {
        logger.LogInformation("{Player} bought offer {Offer} for {Credits} credits", e.PlayerId, e.OfferId, e.CostCredits);

        return ValueTask.CompletedTask;
    }
}
```

Events: `PurchaseCompletedEvent` (`Turbo.Primitives.Catalog.Events`), `PlayerLoggedInEvent`,
`PlayerLoginFailedEvent`, `PlayerRegisteredEvent`, `PlayerRegistrationFailedEvent`,
`PlayerIdentityLinkedEvent` (`Turbo.Primitives.Players.Events`), `RoomEnteredEvent`
(`Turbo.Primitives.Rooms.Events`), `HotelAvailabilityChangedEvent`. A `RemoteAddress` is personal
data: never log it. A plugin raises its own through `IEventPublisher` (host service).

Room events (`IRoomEventListener`, in the room's turn) are a different thing: see turbo-plugin-basics.

## Play policies

`IPlayPolicy` (`docs/plugin-web-routes.md` §3) allows or refuses the site's Play and the client's
login, with a reason and an address to send them to.

## Test

Call the policy directly with a `FakeHotel` (`hotel.Players.Add("Alice")`, `player.Club`...) and a
request record; publish an event to a handler yourself. Guide: `plugin-policies.md`.
