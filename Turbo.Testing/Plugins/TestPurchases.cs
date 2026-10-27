using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Hotel;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Policies;

namespace Turbo.Testing;

/// <summary>A purchase the test made: who, what the policies were asked, what they decided, and whether a plugin handed it over.</summary>
public sealed record TestPurchase(
    TestPlayer Buyer,
    PurchaseRequest Request,
    PurchaseDecision Decision,
    IPurchaseFulfilment? FulfilledBy
);

/// <summary>
/// Purchases as the catalogue makes them, up to the money: the offer's price for the quantity is
/// proposed, every plugin's purchase policy is asked in turn as the hotel asks them (a refusal
/// ends it, a policy that throws refuses it), and an allowed purchase for oneself or a room ad is
/// handed to the plugin fulfilment that takes it, if one does. No money moves and nothing is
/// given here: the decision is what the hotel would charge and give.
/// </summary>
public sealed class TestPurchases
{
    private readonly TestHotel _hotel;
    private readonly List<TestPurchase> _made = [];

    internal TestPurchases(TestHotel hotel) => _hotel = hotel;

    /// <summary>Every purchase made, in order.</summary>
    public IReadOnlyList<TestPurchase> Made => _made;

    /// <summary>
    /// <paramref name="buyer"/> buys <paramref name="offer"/> (<see cref="FakeCatalog.AddOffer"/>)
    /// the way <paramref name="kind"/> says: for themselves, as a gift to <paramref name="giftTo"/>,
    /// as a club membership...
    /// </summary>
    public Task<PurchaseDecision> BuyAsync(
        TestPlayer buyer,
        HotelOffer offer,
        int quantity = 1,
        PurchaseKind kind = PurchaseKind.Catalog,
        TestPlayer? giftTo = null,
        string extraParam = "",
        CancellationToken ct = default
    ) => BuyAsync(buyer, Offer(offer), quantity, kind, giftTo, extraParam, CatalogType.Normal, ct);

    /// <summary><paramref name="buyer"/> buys an offer as the catalogue holds it.</summary>
    public async Task<PurchaseDecision> BuyAsync(
        TestPlayer buyer,
        CatalogOfferSnapshot offer,
        int quantity = 1,
        PurchaseKind kind = PurchaseKind.Catalog,
        TestPlayer? giftTo = null,
        string extraParam = "",
        CatalogType catalogType = CatalogType.Normal,
        CancellationToken ct = default
    )
    {
        var request = new PurchaseRequest(
            buyer.Id,
            kind,
            catalogType,
            offer,
            quantity,
            giftTo?.Id,
            null,
            extraParam
        );
        var decision = await _hotel
            .Policies.DecidePurchaseAsync(
                request,
                new PurchaseDecision
                {
                    CostCredits = offer.CostCredits * quantity,
                    CostSilver = offer.CostSilver * quantity,
                    CostActivityPoints = offer.CostCurrency * quantity,
                    Units = kind == PurchaseKind.Gift ? 1 : quantity,
                },
                ct
            )
            .ConfigureAwait(false);
        IPurchaseFulfilment? fulfilment = null;

        if (
            decision.Allowed
            && kind is PurchaseKind.Catalog or PurchaseKind.ClubMembership or PurchaseKind.RoomAd
        )
        {
            fulfilment = _hotel.Policies.FulfilmentFor(request);

            if (fulfilment is not null)
                await fulfilment.FulfilAsync(request, decision, ct).ConfigureAwait(false);
        }

        _made.Add(new TestPurchase(buyer, request, decision, fulfilment));

        return decision;
    }

    /// <summary>A catalogue offer of the hotel API's, as the catalogue holds it.</summary>
    public static CatalogOfferSnapshot Offer(HotelOffer offer) =>
        Offer(
            offer.Id,
            offer.CostCredits,
            0,
            offer.CostCurrency,
            offer.ActivityPointType,
            offer.PageId,
            offer.Name
        );

    /// <summary>A catalogue offer as the catalogue holds it.</summary>
    public static CatalogOfferSnapshot Offer(
        int id,
        int credits = 0,
        int silver = 0,
        int activityPoints = 0,
        int? activityPointType = null,
        int pageId = 1,
        string name = "offer",
        int clubLevel = 0,
        bool canGift = true
    ) =>
        new()
        {
            Id = id,
            PageId = pageId,
            LocalizationId = name,
            Rentable = false,
            CostCredits = credits,
            CostSilver = silver,
            CostCurrency = activityPoints,
            ActivityPointType = activityPointType,
            CanGift = canGift,
            CanBundle = false,
            ClubLevel = clubLevel,
            Visible = true,
            ProductIds = [],
            Products = ImmutableArray<CatalogProductSnapshot>.Empty,
        };
}
