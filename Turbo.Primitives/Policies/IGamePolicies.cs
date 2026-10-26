using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Policies;

/// <summary>
/// The host's side of the game policies (<see cref="IPurchasePolicy"/>,
/// <see cref="IRoomEntryPolicy"/>, <see cref="IPlacementPolicy"/>, <see cref="IRegistrationPolicy"/>,
/// <see cref="IAvatarEffectPolicy"/>, <see cref="IPurchaseFulfilment"/>): every registered one of a kind,
/// run in order (<c>Order</c>, then plugin key, then as registered). A purchase's and an avatar
/// effect's policies each take the decision of the ones before; for room entry, placement and
/// registration the first refusal is the answer; the first purchase fulfilment that takes an
/// offer fulfils it. With
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

    ValueTask<RegistrationDecision> DecideRegistrationAsync(
        RegistrationRequest request,
        CancellationToken ct
    );

    /// <summary>
    /// The effect the avatar wears when the hotel puts <see cref="AvatarEffectRequest.EffectId"/>
    /// on it (<see cref="IAvatarEffectPolicy.Wear"/>, each on what the one before decided); that
    /// effect with none. Runs on the room's turn.
    /// </summary>
    int DecideAvatarEffect(AvatarEffectRequest request);

    /// <summary>Whether any policy decides what players are shown (<see cref="ShowAvatarEffect"/>); false sends every player the same.</summary>
    bool ShowsAvatarEffects { get; }

    /// <summary>
    /// The effect one player is shown on an avatar (<see cref="IAvatarEffectPolicy.Show"/>, each on
    /// what the one before decided); the one it wears with none. Runs on the room's turn.
    /// </summary>
    int ShowAvatarEffect(AvatarEffectView view);

    /// <summary>
    /// The purchase fulfilment that hands this purchase over instead of the hotel
    /// (<see cref="IPurchaseFulfilment.Fulfils"/>, the first that takes it), or null for the hotel's own.
    /// </summary>
    IPurchaseFulfilment? FulfilmentFor(PurchaseRequest request);

    /// <summary>Every registered policy, in the order it runs within its kind.</summary>
    ImmutableArray<RegisteredPolicy> Registered { get; }
}

/// <summary>
/// One registered policy: its kind (<c>purchase</c>, <c>room-entry</c>, <c>placement</c>,
/// <c>registration</c>, <c>avatar-effect</c>, <c>purchase-fulfilment</c>), the
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
