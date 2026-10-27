using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Policies;

/// <summary>
/// A policy over a catalogue purchase, asked after the hotel's own checks (the offer, the club
/// level) and before any money moves: it lets the purchase go on as proposed, changes what it
/// costs or how many items it gives, or refuses it with a reason the buyer is shown. A public
/// class implementing it is found in a plugin's assembly as it loads and taken away as it
/// unloads; see <see cref="IGamePolicies"/> for the order several run in. Asked for offers bought
/// for oneself (the catalogue and the builders club catalogue, a membership from the club centre
/// included), as a gift, for a room ad (<see cref="PurchaseRequest.RoomAd"/>, which gives the ad and
/// the offer's badge, never more units), for a club gift a member picks and as a player enters a
/// limited edition's sale; <see cref="PurchaseRequest.Kind"/> says which.
/// <para>
/// It runs on the buyer's purchase turn: it may read anything, but must not buy for the same
/// player. A policy that throws refuses the purchase (the buyer gets the hotel's purchase error,
/// nothing is charged) and is logged; the policies after it are not asked.
/// </para>
/// </summary>
public interface IPurchasePolicy
{
    /// <summary>Lower runs first. 0 by default.</summary>
    int Order => 0;

    /// <param name="proposed">
    /// What the policies before this one decided, the hotel's own price and amount at the start.
    /// Return it to let it stand, <c>proposed with { ... }</c> to change it, or
    /// <see cref="PurchaseDecision.Refuse"/>.
    /// </param>
    ValueTask<PurchaseDecision> DecideAsync(
        PurchaseRequest request,
        PurchaseDecision proposed,
        CancellationToken ct
    );
}

/// <summary>
/// A player buying an offer: which way (<paramref name="Kind"/>), <paramref name="Quantity"/> as
/// they asked for it (the price is for that many), the player it is a gift for, if it is one, the
/// room ad it buys, if it is one, and the offer's extra parameter as the client sent it (a pet's
/// name, a trophy's inscription, a group's id; empty when none).
/// </summary>
public sealed record PurchaseRequest(
    PlayerId PlayerId,
    PurchaseKind Kind,
    CatalogType CatalogType,
    CatalogOfferSnapshot Offer,
    int Quantity,
    PlayerId? GiftReceiverId = null,
    RoomAdRequest? RoomAd = null,
    string ExtraParam = ""
);

/// <summary>Which way a player is buying an offer (<see cref="PurchaseRequest.Kind"/>).</summary>
public enum PurchaseKind
{
    /// <summary>Bought for oneself from the catalogue or the builders club catalogue.</summary>
    Catalog,

    /// <summary>
    /// An offer that gives a club membership, bought for oneself: from the catalogue's club page or
    /// the club centre's extend buttons.
    /// </summary>
    ClubMembership,

    /// <summary>Bought for another player (<see cref="PurchaseRequest.GiftReceiverId"/>).</summary>
    Gift,

    /// <summary>A room ad (<see cref="PurchaseRequest.RoomAd"/>).</summary>
    RoomAd,

    /// <summary>
    /// A club gift a member picks off the club gift shelf. It costs nothing, so only a refusal is
    /// read.
    /// </summary>
    ClubGiftClaim,

    /// <summary>
    /// A player entering a limited edition's sale (its raffle, or a sale after it). Only a refusal is
    /// read: a sale won is charged the offer's price.
    /// </summary>
    LimitedEdition,
}

/// <summary>
/// A room ad (a room event, <c>PurchaseRoomAdMessage</c>) its room's owner buys: for which room,
/// in which event category, its name and description, whether it extends the one running, and
/// how long it runs.
/// </summary>
public sealed record RoomAdRequest(
    RoomId RoomId,
    int CategoryId,
    string Name,
    string Description,
    bool Extended,
    TimeSpan Duration
);

/// <summary>
/// What a purchase costs in all and gives, or that it is refused. The costs are the whole
/// purchase's (the offer's price times the quantity); <see cref="CostActivityPoints"/> is in the
/// offer's activity point currency. <see cref="Units"/> is how many of the offer the buyer gets,
/// bulk-buy bonus included; a gift always holds one, so it is not read for one.
/// </summary>
public sealed record PurchaseDecision
{
    public bool Allowed { get; init; } = true;

    /// <summary>Why it is refused, shown to the buyer; null shows the hotel's own refusal.</summary>
    public string? Reason { get; init; }

    /// <summary>
    /// The hotel's purchase error a refusal without a <see cref="Reason"/> shows (club required, not
    /// enough credits, ...); <see cref="CatalogPurchaseErrorType.PurchaseFailed"/> by default.
    /// </summary>
    public CatalogPurchaseErrorType Error { get; init; } = CatalogPurchaseErrorType.PurchaseFailed;

    public int CostCredits { get; init; }

    public int CostSilver { get; init; }

    public int CostActivityPoints { get; init; }

    public int Units { get; init; } = 1;

    public static PurchaseDecision Refuse(
        string? reason = null,
        CatalogPurchaseErrorType error = CatalogPurchaseErrorType.PurchaseFailed
    ) =>
        new()
        {
            Allowed = false,
            Reason = reason,
            Error = error,
        };

    /// <summary>
    /// The decision as the hotel takes it: no cost below nothing, at least one unit, and a refusal
    /// always an error.
    /// </summary>
    public PurchaseDecision Clamped() =>
        this with
        {
            Error =
                Error == CatalogPurchaseErrorType.None
                    ? CatalogPurchaseErrorType.PurchaseFailed
                    : Error,
            CostCredits = Math.Max(0, CostCredits),
            CostSilver = Math.Max(0, CostSilver),
            CostActivityPoints = Math.Max(0, CostActivityPoints),
            Units = Math.Max(1, Units),
        };
}
