using FluentAssertions;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Furniture;
using Turbo.Gamedata.Furniture;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture.Enums;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Gamedata;

/// <summary>
/// A furniture's furnidata offer ids come from the catalogs players see: <c>offerid</c> from an
/// offer they can reach in the normal catalog, <c>bcofferid</c> from one in the Builders Club
/// catalog, both from a page both show - and none from a hidden page, a page under one, a hidden
/// offer, or a bundle.
/// </summary>
public sealed class FurnitureOfferStampsTests : IDisposable
{
    private const int REGULAR_PAGE = 30;
    private const int BUILDERS_ONLY_PAGE = 31;
    private const int BOTH_PAGE = 32;
    private const int UNDER_HIDDEN_PAGE = 33;

    private const int TABLE = 40;
    private const int LAMP = 41;
    private const int SOFA = 42;
    private const int RUG = 43;
    private const int PLANT = 44;
    private const int BED = 45;

    private readonly Catalog.CatalogFixture _catalog = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public FurnitureOfferStampsTests()
    {
        _catalog.Db.Insert(Page(REGULAR_PAGE, FURNITURE, "Regular", sort: 2));
        _catalog.Db.Insert(
            Page(BUILDERS_ONLY_PAGE, FURNITURE, "Builders", CatalogPageDisplay.BuildersClubOnly, 3)
        );
        _catalog.Db.Insert(Page(BOTH_PAGE, FURNITURE, "Both", CatalogPageDisplay.Both, 4));
        // Shown in both catalogs itself, but under the invisible tab.
        _catalog.Db.Insert(
            Page(UNDER_HIDDEN_PAGE, HIDDEN_PAGE, "Under hidden", CatalogPageDisplay.Both)
        );

        foreach (var id in new[] { TABLE, LAMP, SOFA, RUG, PLANT, BED })
            _catalog.Db.Insert(Definition(id));
    }

    public void Dispose() => _catalog.Dispose();

    [Fact]
    public async Task an_offer_on_a_regular_page_stamps_offerid_only()
    {
        Offer(500, REGULAR_PAGE, TABLE);

        var (offers, buildersClub) = await StampsAsync();

        offers.Should().Contain(TABLE, 500);
        buildersClub.Should().NotContainKey(TABLE);
    }

    [Fact]
    public async Task an_offer_on_a_builders_club_page_stamps_bcofferid_only()
    {
        Offer(501, BUILDERS_ONLY_PAGE, LAMP);

        var (offers, buildersClub) = await StampsAsync();

        offers.Should().NotContainKey(LAMP);
        buildersClub.Should().Contain(LAMP, 501);
    }

    [Fact]
    public async Task an_offer_on_a_page_both_catalogs_show_stamps_both_with_the_same_offer()
    {
        Offer(502, BOTH_PAGE, SOFA);

        var (offers, buildersClub) = await StampsAsync();

        offers.Should().Contain(SOFA, 502);
        buildersClub.Should().Contain(SOFA, 502);
    }

    [Fact]
    public async Task a_hidden_page_its_children_and_hidden_offers_stamp_nothing()
    {
        Offer(503, HIDDEN_PAGE, RUG);
        Offer(504, UNDER_HIDDEN_PAGE, RUG);
        Offer(505, REGULAR_PAGE, RUG, visible: false);

        var (offers, buildersClub) = await StampsAsync();

        offers.Should().NotContainKey(RUG);
        buildersClub.Should().NotContainKey(RUG);
    }

    [Fact]
    public async Task a_bundle_is_not_the_furnitures_offer()
    {
        Offer(506, REGULAR_PAGE, PLANT, alsoSells: BED);

        var (offers, _) = await StampsAsync();

        offers.Should().NotContainKey(PLANT);
        offers.Should().NotContainKey(BED);
    }

    [Fact]
    public async Task a_single_item_wins_over_a_pack_then_the_oldest_offer()
    {
        Offer(507, REGULAR_PAGE, BED, quantity: 5);
        Offer(509, REGULAR_PAGE, BED);
        Offer(508, BOTH_PAGE, BED);

        var (offers, _) = await StampsAsync();

        offers.Should().Contain(BED, 508);
    }

    [Fact]
    public async Task the_fixtures_chair_is_stamped_from_its_first_reachable_visible_offer()
    {
        // SOLD is the oldest; the hidden offer and the offer on the hidden page come between.
        var (offers, _) = await StampsAsync();

        offers.Should().Contain(CHAIR, SOLD);
    }

    private async Task<(
        Dictionary<int, int> Offers,
        Dictionary<int, int> BuildersClub
    )> StampsAsync()
    {
        var normal = _catalog.NormalProvider();
        var buildersClub = _catalog.BuildersClubProvider();

        await normal.ReloadAsync(Ct);
        await buildersClub.ReloadAsync(Ct);

        return (
            FurnitureOfferStamps.Of(normal.Current),
            FurnitureOfferStamps.Of(buildersClub.Current)
        );
    }

    private void Offer(
        int id,
        int pageId,
        int definitionId,
        int quantity = 1,
        bool visible = true,
        int? alsoSells = null
    )
    {
        _catalog.Db.Insert(
            new CatalogOfferEntity
            {
                Id = id,
                CatalogPageEntityId = pageId,
                LocalizationId = "furni",
                CostCredits = 1,
                CostCurrency = 0,
                CanGift = true,
                CanBundle = true,
                ClubLevel = 0,
                Visible = visible,
                Page = null!,
            }
        );
        _catalog.Db.Insert(Product(id * 10, id, definitionId, quantity));

        if (alsoSells is { } other)
            _catalog.Db.Insert(Product(id * 10 + 1, id, other, 1));
    }

    private static CatalogProductEntity Product(
        int id,
        int offerId,
        int definitionId,
        int quantity
    ) =>
        new()
        {
            Id = id,
            CatalogOfferEntityId = offerId,
            ProductType = ProductType.Floor,
            FurnitureDefinitionEntityId = definitionId,
            Quantity = quantity,
            Offer = null!,
        };

    private static FurnitureDefinitionEntity Definition(int id) =>
        new()
        {
            Id = id,
            SpriteId = id,
            Name = $"furni_{id}",
            ProductType = ProductType.Floor,
            FurniCategory = FurnitureCategory.Default,
            Logic = "default_floor",
            Width = 1,
            Length = 1,
            StackHeight = 1,
            CanStack = true,
            CanWalk = false,
            CanSit = false,
            CanLay = false,
            CanRecycle = true,
            CanTrade = true,
            CanGroup = true,
            CanSell = true,
        };
}
