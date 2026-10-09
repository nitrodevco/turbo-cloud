using FluentAssertions;
using Turbo.Admin.Catalog;
using Turbo.Database.Entities.Catalog;
using Turbo.Primitives.Furniture.Enums;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// The catalog editor's audits: the furni the catalog doesn't sell - in no offer, or only where
/// players can't see it - leaving out what builders sell (patterns, posters, pets); and the furni
/// sold alone by more than one offer, with where each offer is and whether players see it.
/// </summary>
public sealed class AdminCatalogAuditTests : IDisposable
{
    private const int LAMP = 20;
    private const int PATTERN = 21;
    private const int RUG = 22;
    private const int SOFA = 23;

    private readonly CatalogFixture _catalog = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AdminCatalogAuditTests()
    {
        _catalog.AddDefinition(LAMP, "lamp_basic", set: x => x.FurniLine = "basic");
        _catalog.AddDefinition(
            PATTERN,
            "wallpaper",
            ProductType.Wall,
            x => x.FurniCategory = FurnitureCategory.WallPaper
        );
        _catalog.AddDefinition(RUG, "rug_red", set: x => x.ClientCategory = "rug");
        _catalog.AddDefinition(SOFA, "sofa_red");
        Sell(200, HIDDEN_PAGE, true, RUG);
        Sell(201, CHILD, false, SOFA);
    }

    public void Dispose() => _catalog.Dispose();

    private AdminCatalogAudit Audit => new(_catalog.Db);

    private void Sell(int offerId, int pageId, bool visible, params int[] definitions)
    {
        _catalog.Db.Insert(
            new CatalogOfferEntity
            {
                Id = offerId,
                CatalogPageEntityId = pageId,
                LocalizationId = "x",
                CostCredits = 1,
                CostCurrency = 0,
                CanGift = true,
                CanBundle = true,
                ClubLevel = 0,
                Visible = visible,
                Page = null!,
            }
        );

        for (var i = 0; i < definitions.Length; i++)
        {
            _catalog.Db.Insert(
                new CatalogProductEntity
                {
                    Id = offerId * 10 + i,
                    CatalogOfferEntityId = offerId,
                    ProductType = ProductType.Floor,
                    FurnitureDefinitionEntityId = definitions[i],
                    Quantity = 1,
                    Offer = null!,
                }
            );
        }
    }

    [Fact]
    public async Task Missing_ListsTheFurniNoOfferSells_ButWhatBuildersSell()
    {
        var result = await Audit.GetUnofferedAsync("missing", null, null, null, 0, 100, Ct);

        result.Items.Select(x => x.Name).Should().Equal("lamp_basic", "poster");
        result.Total.Should().Be(2);
        result
            .Lines.Select(x => (x.Value, x.Count))
            .Should()
            .BeEquivalentTo([("basic", 1), ("", 1)]);
    }

    [Fact]
    public async Task Hidden_ListsTheFurniSoldOnlyWherePlayersCantSeeIt()
    {
        var result = await Audit.GetUnofferedAsync("hidden", null, null, null, 0, 100, Ct);

        result.Items.Select(x => x.Name).Should().BeEquivalentTo(["rug_red", "sofa_red"]);
    }

    [Fact]
    public async Task Unoffered_IsNarrowedByText_LineAndCategory()
    {
        (await Audit.GetUnofferedAsync("missing", "LAMP", null, null, 0, 100, Ct))
            .Items.Select(x => x.Id)
            .Should()
            .Equal(LAMP);
        (await Audit.GetUnofferedAsync("missing", null, "basic", null, 0, 100, Ct))
            .Items.Select(x => x.Id)
            .Should()
            .Equal(LAMP);
        (await Audit.GetUnofferedAsync("hidden", null, null, "rug", 0, 100, Ct))
            .Items.Select(x => x.Id)
            .Should()
            .Equal(RUG);
    }

    [Fact]
    public async Task Duplicates_ListEachOfferOfAFurniSoldAloneMoreThanOnce_WithWherePlayersSeeIt()
    {
        Sell(202, CHILD, true, CHAIR, SOFA);

        var result = await Audit.GetDuplicatesAsync(Ct);

        var chair = result.Items.Should().ContainSingle().Which;

        chair.Furni.Id.Should().Be(CHAIR);
        chair
            .Offers.Select(x => x.OfferId)
            .Should()
            .Equal(SOLD, HIDDEN_OFFER, ON_HIDDEN_PAGE, IN_DUCKETS, IN_DIAMONDS, IN_CREDITS_ROW);
        chair
            .Offers.Where(x => !x.Shown)
            .Select(x => x.OfferId)
            .Should()
            .Equal(HIDDEN_OFFER, ON_HIDDEN_PAGE);
        chair.Offers.First().PagePath.Should().Be("Furniture");
    }
}
