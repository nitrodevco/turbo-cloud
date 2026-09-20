using System.Diagnostics.CodeAnalysis;
using Orleans;

namespace Turbo.Primitives.Catalog.Snapshots;

/// <summary>
/// The renewal the client is offered from the club centre: a <see cref="ClubOfferSnapshot"/> plus
/// what it would have cost without the discount, so the client can draw the saving
/// (<c>ClubOfferExtendData.discountCreditAmount</c> multiplies the per-period price by the
/// offer's months, so the two price fields are per period, not per offer).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ClubExtendOfferSnapshot : ClubOfferSnapshot
{
    public ClubExtendOfferSnapshot() { }

    [SetsRequiredMembers]
    public ClubExtendOfferSnapshot(ClubOfferSnapshot offer)
        : base(offer) { }

    [Id(0)]
    public required int OriginalPriceCreditsPerPeriod { get; init; }

    [Id(1)]
    public required int OriginalPriceActivityPointsPerPeriod { get; init; }

    [Id(2)]
    public required int OriginalActivityPointType { get; init; }

    /// <summary>Days the player has left before the renewal is applied.</summary>
    [Id(3)]
    public required int SubscriptionDaysLeft { get; init; }
}
