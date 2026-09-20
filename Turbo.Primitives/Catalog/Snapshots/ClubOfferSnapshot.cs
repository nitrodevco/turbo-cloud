using System;
using Orleans;

namespace Turbo.Primitives.Catalog.Snapshots;

/// <summary>
/// A Habbo Club membership on sale. It is a catalog offer whose product grants subscription days
/// rather than furniture, projected out of the catalog snapshot by
/// <c>ICatalogService.GetClubOffers</c>.
/// </summary>
[GenerateSerializer, Immutable]
public record ClubOfferSnapshot
{
    public ClubOfferSnapshot() { }

    [Id(0)]
    public required int OfferId { get; init; }

    /// <summary>The offer's localization id, which the client shows as the product code.</summary>
    [Id(1)]
    public required string ProductCode { get; init; }

    [Id(2)]
    public required int PriceCredits { get; init; }

    [Id(3)]
    public required int PriceActivityPoints { get; init; }

    [Id(4)]
    public required int PriceActivityPointType { get; init; }

    /// <summary>
    /// Always true for an active membership. This client collapses the two tiers —
    /// <c>HabboClubLevelEnum.HasVip</c> is <c>level &gt;= 1</c> and <c>SessionDataManager</c> maps
    /// any non-zero club level to VIP — and it gates group creation on this flag
    /// (<c>HabboGroupsManager</c>), so a basic tier would only take rights away for nothing.
    /// </summary>
    [Id(5)]
    public required bool IsVip { get; init; }

    /// <summary>Whole periods this offer grants; the client draws <c>months * 31 + extraDays</c>.</summary>
    [Id(6)]
    public required int Months { get; init; }

    [Id(7)]
    public required int ExtraDays { get; init; }

    [Id(8)]
    public required bool IsGiftable { get; init; }

    /// <summary>Days the player would hold in total if they bought this now.</summary>
    [Id(9)]
    public required int DaysLeftAfterPurchase { get; init; }

    /// <summary>The date the membership would end, which the client shows as-is.</summary>
    [Id(10)]
    public required int Year { get; init; }

    [Id(11)]
    public required int Month { get; init; }

    [Id(12)]
    public required int Day { get; init; }

    /// <summary>Days this offer grants, which is what the purchase actually applies.</summary>
    [Id(13)]
    public required int Days { get; init; }

    /// <summary>
    /// Fills in the four fields that depend on the buyer rather than the offer. A membership is
    /// extended from its current end when it is still running and from now when it is not, so
    /// this has to agree with <c>IPlayerSubscriptionGrain.ExtendAsync</c>.
    /// </summary>
    public ClubOfferSnapshot ForSubscription(DateTime? expiresAt, DateTime now)
    {
        var from = expiresAt is { } end && end > now ? end : now;
        var ends = from.AddDays(Days);

        return this with
        {
            DaysLeftAfterPurchase = (int)Math.Ceiling((ends - now).TotalDays),
            Year = ends.Year,
            Month = ends.Month,
            Day = ends.Day,
        };
    }
}
