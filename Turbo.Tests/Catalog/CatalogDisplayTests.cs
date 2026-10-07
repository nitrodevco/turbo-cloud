using FluentAssertions;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// Both catalogs are cut from one tree of pages by each page's display. A page is in a catalog
/// when it is shown there, and so is every page above it, which then leads to it without selling
/// its own offers. The Builders Club catalog has no tabs: what is under every tab is gathered
/// under the one category the client opens.
/// </summary>
public sealed class CatalogDisplayTests : IDisposable
{
    private const int BY_DESIGN = 20;
    private const int ANNA = 21;
    private const int BASE = 22;
    private const int BATHROOM = 23;
    private const int UNDER_SECRET = 24;

    private const int ON_BY_DESIGN = 300;
    private const int ON_ANNA = 301;
    private const int ON_BASE = 302;
    private const int ON_BATHROOM = 303;

    private readonly CatalogFixture _catalog = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public CatalogDisplayTests()
    {
        // Furniture > By Design (regular) > Anna (bc only), Base (regular), Bathroom (both).
        _catalog.Db.Insert(Page(BY_DESIGN, FURNITURE, "By Design", sort: 2));
        _catalog.Db.Insert(Page(ANNA, BY_DESIGN, "Anna", CatalogPageDisplay.BuildersClubOnly, 0));
        _catalog.Db.Insert(Page(BASE, BY_DESIGN, "Base", CatalogPageDisplay.Regular, 1));
        _catalog.Db.Insert(Page(BATHROOM, BY_DESIGN, "Bathroom", CatalogPageDisplay.Both, 2));
        // Under the invisible tab, which hides it in both catalogs.
        _catalog.Db.Insert(Page(UNDER_SECRET, HIDDEN_PAGE, "Hidden", CatalogPageDisplay.Both));

        _catalog.AddOffer(ON_BY_DESIGN, BY_DESIGN, credits: 1);
        _catalog.AddOffer(ON_ANNA, ANNA, credits: 1);
        _catalog.AddOffer(ON_BASE, BASE, credits: 1);
        _catalog.AddOffer(ON_BATHROOM, BATHROOM, credits: 1);
    }

    public void Dispose() => _catalog.Dispose();

    private async Task<CatalogSnapshot> NormalAsync()
    {
        var provider = _catalog.NormalProvider();

        await provider.ReloadAsync(Ct);

        return provider.Current;
    }

    private async Task<CatalogSnapshot> BuildersClubAsync()
    {
        var provider = _catalog.BuildersClubProvider();

        await provider.ReloadAsync(Ct);

        return provider.Current;
    }

    [Fact]
    public async Task TheNormalCatalog_ShowsTheRegularAndBothPages_AndNotTheBuildersClubOnes()
    {
        var snapshot = await NormalAsync();

        snapshot.RootPageId.Should().Be(ROOT);
        snapshot.PagesById[ROOT].ChildIds.Should().Equal(FURNITURE, HIDDEN_PAGE);
        snapshot.PagesById[FURNITURE].ChildIds.Should().Equal(CHILD, BY_DESIGN);
        snapshot.PagesById[BY_DESIGN].ChildIds.Should().Equal(BASE, BATHROOM);
        snapshot.PagesById[BY_DESIGN].OfferIds.Should().Equal(ON_BY_DESIGN);
        snapshot.PagesById.Should().NotContainKeys(ANNA, BUILDERS_PAGE);
        snapshot.OffersById.Should().NotContainKey(ON_ANNA).And.ContainKey(ON_BATHROOM);
        // An invisible page is still there, hidden, for what opens it by name.
        snapshot.PagesById[HIDDEN_PAGE].Visible.Should().BeFalse();
        snapshot.PagesById[HIDDEN_PAGE].OfferIds.Should().Contain(ON_HIDDEN_PAGE);
    }

    [Fact]
    public async Task TheBuildersClubCatalog_HasNoTabs_AndLeadsToItsPagesThroughTheirParents()
    {
        var snapshot = await BuildersClubAsync();

        // The root holds the one category the client opens, in place of tabs.
        snapshot.RootPageId.Should().Be(ROOT);
        var category = snapshot.PagesById[ROOT].ChildIds.Should().ContainSingle().Subject;

        snapshot.PagesById.Should().NotContainKeys(FURNITURE, HIDDEN_PAGE, UNDER_SECRET, BASE);
        snapshot.PagesById[category].ChildIds.Should().Equal(BUILDERS_PAGE, BY_DESIGN);

        // By Design is regular only, but leads to Anna and Bathroom, so it is there too.
        var byDesign = snapshot.PagesById[BY_DESIGN];

        byDesign.Visible.Should().BeTrue();
        byDesign.ChildIds.Should().Equal(ANNA, BATHROOM);
        byDesign.OfferIds.Should().BeEmpty("it only leads to the pages below it here");
        snapshot.PagesById[ANNA].OfferIds.Should().Equal(ON_ANNA);
        snapshot.PagesById[BATHROOM].OfferIds.Should().Equal(ON_BATHROOM);

        // Only what this catalog shows can be placed from it.
        snapshot
            .OffersById.Keys.Should()
            .BeEquivalentTo([ON_ANNA, ON_BATHROOM], "Base and By Design are regular only");
    }
}
