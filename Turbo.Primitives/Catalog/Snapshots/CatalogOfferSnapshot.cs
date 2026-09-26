using System.Collections.Generic;
using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Primitives.Catalog.Snapshots;

[GenerateSerializer, Immutable]
public sealed record CatalogOfferSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required int PageId { get; init; }

    [Id(2)]
    public required string LocalizationId { get; init; }

    [Id(3)]
    public required bool Rentable { get; init; }

    [Id(4)]
    public required int CostCredits { get; init; }

    [Id(5)]
    public required int CostSilver { get; init; }

    [Id(6)]
    public required int CostCurrency { get; init; }

    [Id(7)]
    public required int? CurrencyTypeId { get; init; }

    [Id(8)]
    public required bool CanGift { get; init; }

    [Id(9)]
    public required bool CanBundle { get; init; }

    [Id(10)]
    public required int ClubLevel { get; init; }

    /// <summary>
    /// Whether only a club member may buy this. This client collapses the club tiers (see
    /// <see cref="ClubOfferSnapshot.IsVip"/>), so any level above none asks for an active
    /// membership, and any active membership meets it.
    /// </summary>
    public bool RequiresClub => ClubLevel > 0;

    [Id(11)]
    public required bool Visible { get; init; }

    [Id(12)]
    public required ImmutableArray<int> ProductIds { get; init; }

    [Id(13)]
    public required ImmutableArray<CatalogProductSnapshot> Products { get; init; }

    /// <summary>
    /// Days of used-up Habbo Club membership needed before this offer may be picked as a club
    /// gift; null when it is not one.
    /// </summary>
    [Id(14)]
    public int? ClubGiftDaysRequired { get; init; }

    /// <summary>
    /// What buying <paramref name="quantity"/> of this offer takes out of a wallet, one request
    /// per currency it costs. The one reading of an offer's price: the raffle had its own copy,
    /// which left out silver and handed silver-priced LTDs out for free.
    /// </summary>
    public List<WalletDebitRequest> ToDebitRequests(int quantity)
    {
        var requests = new List<WalletDebitRequest>(3);

        if (CostCredits > 0)
            requests.Add(
                new WalletDebitRequest
                {
                    CurrencyKind = CurrencyKind.Credits,
                    Amount = CostCredits * quantity,
                }
            );

        if (CostSilver > 0)
            requests.Add(
                new WalletDebitRequest
                {
                    CurrencyKind = CurrencyKind.Silver,
                    Amount = CostSilver * quantity,
                }
            );

        if (CostCurrency > 0)
            requests.Add(
                new WalletDebitRequest
                {
                    CurrencyKind = CurrencyKind.ActivityPoints(CurrencyTypeId),
                    Amount = CostCurrency * quantity,
                }
            );

        return requests;
    }
}
