using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Policies;

/// <summary>
/// The host's side of the game policies (<see cref="IPurchasePolicy"/>,
/// <see cref="IRoomEntryPolicy"/>, <see cref="IPlacementPolicy"/>): every registered one of a kind,
/// run in order (<c>Order</c>, then plugin key, then as registered). A purchase's policies each
/// take the decision of the ones before; for the other kinds the first refusal is the answer. With
/// none the hotel decides as it always does. Two plugins with a policy of the same kind both run,
/// in that order; the host logs it as a conflict and lists it on each plugin
/// (<see cref="Registered"/>), since the later one decides on what the earlier one left.
/// </summary>
public interface IGamePolicies
{
    ValueTask<PurchaseDecision> DecidePurchaseAsync(
        PurchaseRequest request,
        PurchaseDecision proposed,
        CancellationToken ct
    );

    ValueTask<RoomEntryDecision> DecideRoomEntryAsync(
        RoomEntryRequest request,
        CancellationToken ct
    );

    ValueTask<PlacementDecision> DecidePlacementAsync(
        PlacementRequest request,
        CancellationToken ct
    );

    /// <summary>Every registered policy, in the order it runs within its kind.</summary>
    ImmutableArray<RegisteredPolicy> Registered { get; }
}

/// <summary>
/// One registered policy: its kind (<c>purchase</c>, <c>room-entry</c>, <c>placement</c>), the
/// plugin that registered it (<c>core</c> for the host's own), its class and order, and the other
/// owners with a policy of the same kind (empty when it has the kind to itself).
/// </summary>
public sealed record RegisteredPolicy(
    string Kind,
    string Owner,
    string Policy,
    int Order,
    ImmutableArray<string> SharedWith
);
