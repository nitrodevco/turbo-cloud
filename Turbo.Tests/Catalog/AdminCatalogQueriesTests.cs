using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Admin.Catalog;
using Turbo.Admin.Configuration;
using Turbo.Database.Entities.Catalog;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// The catalog as the editor reads it: every page of one catalog, hidden ones too, with its
/// offer count; a page's offers with what they give and what is left of a limited series; the
/// currencies a price can be in; and the furniture an offer can give.
/// </summary>
public sealed class AdminCatalogQueriesTests : IDisposable
{
    private readonly CatalogFixture _catalog = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _catalog.Dispose();

    private AdminCatalogQueries Queries()
    {
        _catalog.Fakes.Handlers["get_UnpublishedChanges"] = _ => 3;

        return new AdminCatalogQueries(
            _catalog.Db,
            _catalog.Definitions,
            _catalog.Fakes.Create<ICatalogEditService>(),
            Options.Create(new AdminConfig { CatalogLayouts = ["default_3x3", "frontpage4"] })
        );
    }

    [Fact]
    public async Task TheTree_IsEveryPage_WithWhereItIsShown_AndItsOffers()
    {
        var tree = await Queries().GetTreeAsync(canManage: true, Ct);

        tree.RootId.Should().Be(ROOT);
        tree.Pages.Select(x => x.Id)
            .Should()
            .BeEquivalentTo([ROOT, FURNITURE, HIDDEN_PAGE, CHILD, BUILDERS_PAGE]);
        tree.Pages.Single(x => x.Id == FURNITURE).Display.Should().Be("regular");
        tree.Pages.Single(x => x.Id == HIDDEN_PAGE).Display.Should().Be("invisible");
        tree.Pages.Single(x => x.Id == BUILDERS_PAGE).Display.Should().Be("bc_only");
        tree.Pages.Single(x => x.Id == FURNITURE).OfferCount.Should().Be(5);
        tree.UnpublishedChanges.Should().Be(3);
        tree.CanManage.Should().BeTrue();
    }

    [Fact]
    public async Task TheCurrencies_AreTheActivityPointOnes()
    {
        var tree = await Queries().GetTreeAsync(canManage: false, Ct);

        tree.Currencies.Select(x => (x.Id, x.Name, x.ActivityPointType))
            .Should()
            .Equal((DUCKETS_ROW, "duckets", 0), (DIAMONDS_ROW, "diamonds", 5));
        tree.Layouts.Should().Equal("default_3x3", "frontpage4");
    }

    [Fact]
    public async Task APage_HasItsOffers_WithWhatTheyGive_AndWhatIsLeftOfASeries()
    {
        _catalog.Db.Insert(
            new LtdSeriesEntity
            {
                Id = 1,
                CatalogProductEntityId = SOLD,
                TotalQuantity = 100,
                RemainingQuantity = 40,
                RaffleWindowSeconds = 30,
                IsActive = true,
            }
        );

        var page = await Queries().GetPageAsync(FURNITURE, Ct);

        page.Should().NotBeNull();
        page!
            .Offers.Select(x => x.Id)
            .Should()
            .Equal(SOLD, HIDDEN_OFFER, IN_DUCKETS, IN_DIAMONDS, IN_CREDITS_ROW);

        var sold = page.Offers[0].Products.Should().ContainSingle().Subject;

        sold.Type.Should().Be("floor");
        sold.DefinitionName.Should().Be("chair");
        sold.Limited!.Total.Should().Be(100);
        sold.Limited!.Remaining.Should().Be(40);
        page.Offers.Single(x => x.Id == HIDDEN_OFFER).Visible.Should().BeFalse();
        (await Queries().GetPageAsync(999, Ct)).Should().BeNull();
    }

    [Fact]
    public async Task TheClubShop_CountsWhatTheClubWindowSells_AndFindsItsShownPage()
    {
        // The club layout under another name is not what the client opens.
        _catalog.Db.Insert(
            new CatalogPageEntity
            {
                Id = 49,
                ParentEntityId = ROOT,
                Localization = "Club layout, wrong name",
                Name = "habbo_club",
                Icon = 1,
                Layout = AdminCatalogQueries.CLUB_BUY,
                SortOrder = 8,
                Display = CatalogPageDisplay.Regular,
            }
        );
        _catalog.Db.Insert(
            new CatalogPageEntity
            {
                Id = 50,
                ParentEntityId = ROOT,
                Localization = "Habbo Club",
                Name = AdminCatalogQueries.CLUB_PAGE_NAME,
                Icon = 1,
                Layout = AdminCatalogQueries.CLUB_BUY,
                SortOrder = 9,
                Display = CatalogPageDisplay.Regular,
            }
        );
        _catalog.Db.Insert(
            new CatalogOfferEntity
            {
                Id = 200,
                CatalogPageEntityId = HIDDEN_PAGE,
                LocalizationId = "habbo_club_1_month",
                CostCredits = 25,
                CostCurrency = 0,
                CanGift = false,
                CanBundle = false,
                ClubLevel = 0,
                Visible = true,
                Page = null!,
            }
        );
        _catalog.Db.Insert(
            new CatalogProductEntity
            {
                Id = 200,
                CatalogOfferEntityId = 200,
                ProductType = ProductType.HabboClub,
                Quantity = 1,
                SubscriptionType = SubscriptionType.HabboClub,
                SubscriptionDays = 31,
                Offer = null!,
            }
        );

        var club = (await Queries().GetTreeAsync(false, Ct)).Club;

        club.Should().NotBeNull();
        club!.Memberships.Should().Be(1, "on a hidden page, but the club window lists it");
        club.Gifts.Should().Be(0);
        club.ClubBuyPageId.Should().Be(50);
        club.ClubGiftsPageId.Should().BeNull();
    }

    [Fact]
    public void TheFurniture_IsFoundByTheStartOfItsName_OrItsId()
    {
        Queries()
            .SearchFurniture("CHA")
            .Select(x => (x.Id, x.Type))
            .Should()
            .Equal((CHAIR, "floor"));
        Queries().SearchFurniture($"{POSTER}").Select(x => x.Name).Should().Equal("poster");
        Queries().SearchFurniture(" ").Should().BeEmpty();
    }
}
