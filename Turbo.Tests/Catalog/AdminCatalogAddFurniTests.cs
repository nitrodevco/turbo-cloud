using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Catalog;
using Turbo.Catalog.Editing;
using Turbo.Database.Entities.Catalog;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// Furni the audit lists, put on a page in one go: an offer each on the page, or sorted onto pages
/// under it - by kind (the first word of the class name, the line's own name passed over), by
/// colour (a colour word in the public name, or the part colours' tint), or by kind and then each
/// kind by colour - with the kinds or colours too small for a page sharing one; furni all of one
/// kind or colour stay on the page above.
/// </summary>
public sealed class AdminCatalogAddFurniTests : IDisposable
{
    private static readonly PlayerId Editor = 1;

    private readonly CatalogFixture _catalog = new();
    private readonly CatalogEditService _service;
    private int _nextId = 30;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AdminCatalogAddFurniTests()
    {
        _catalog.Fakes.Handlers["GetOnlinePlayerIds"] = _ => (IReadOnlyCollection<PlayerId>)[];
        _service = new CatalogEditService(
            _catalog.Db,
            _catalog.Definitions,
            _catalog.NormalProvider(),
            _catalog.BuildersClubProvider(),
            _catalog.Fakes.Create<ISessionGateway>(),
            _catalog.Fakes.Create<IGrainFactory>(),
            new CapturingLogger<ICatalogEditService>()
        );
    }

    public void Dispose() => _catalog.Dispose();

    private AdminCatalogAddFurni Adder => new(_catalog.Db, _service);

    private int[] Furni(string line, params string[] names) =>
        [
            .. names.Select(name =>
                _catalog.AddDefinition(_nextId++, name, set: x => x.FurniLine = line).Id
            ),
        ];

    /// <summary>Furni of a line, each a class name, a public name and its part colours.</summary>
    private int[] Coloured(
        string line,
        params (string Name, string PublicName, string[]? Parts)[] furni
    ) =>
        [
            .. furni.Select(f =>
                _catalog
                    .AddDefinition(
                        _nextId++,
                        f.Name,
                        set: x =>
                        {
                            x.FurniLine = line;
                            x.PublicName = f.PublicName;
                            x.PartColors = f.Parts?.ToList();
                        }
                    )
                    .Id
            ),
        ];

    private static CatalogAddFurniRequest Request(int[] ids, string? sortBy) =>
        new(ids, 3, 0, null, 0, CanGift: true, Visible: true, sortBy);

    private int PageNamed(int parentId, string title)
    {
        using var db = _catalog.Db.CreateDbContext();

        return db
            .CatalogPages.Single(x => x.ParentEntityId == parentId && x.Localization == title)
            .Id;
    }

    /// <summary>The pages under a page by title, each with the class names its offers give, in order.</summary>
    private Dictionary<string, string[]> PagesUnder(int parentId)
    {
        using var db = _catalog.Db.CreateDbContext();

        return db
            .CatalogPages.AsNoTracking()
            .Where(x => x.ParentEntityId == parentId)
            .ToList()
            .ToDictionary(x => x.Localization, x => NamesOn(db, x.Id));
    }

    private string[] NamesOn(int pageId)
    {
        using var db = _catalog.Db.CreateDbContext();

        return NamesOn(db, pageId);
    }

    private static string[] NamesOn(Turbo.Database.Context.TurboDbContext db, int pageId) =>
        [
            .. db
                .CatalogProducts.AsNoTracking()
                .Where(x => x.Offer.CatalogPageEntityId == pageId)
                .OrderBy(x => x.Offer.SortOrder)
                .ThenBy(x => x.Offer.Id)
                .Select(x => x.FurnitureDefinition!.Name),
        ];

    [Fact]
    public async Task SubPages_PutEachKindOnAPageUnderIt_TheSmallKindsSharingOne()
    {
        var ids = Furni(
            "rare",
            "marquee*1",
            "marquee*2",
            "marquee*3",
            "rare_fan*1",
            "rare_fan*2",
            "rare_fan*3",
            "wooden_screen*1",
            "wooden_screen*2",
            "wooden_screen*3",
            "throne",
            "spyro"
        );

        var plan = await Adder.PlanAsync(CHILD, Request(ids, AdminCatalogAddFurni.SORT_KIND), Ct);

        plan.Value!.Pages.Select(x => (x.Title, x.PageId, x.Count))
            .Should()
            .Equal(
                ("Fan", null, 3),
                ("Marquee", null, 3),
                ("Wooden screen", null, 3),
                ("Other", null, 2)
            );

        var added = await Adder.AddAsync(
            Editor,
            CHILD,
            Request(ids, AdminCatalogAddFurni.SORT_KIND),
            Ct
        );

        added.Value!.Done.Should().Be(11);
        PagesUnder(CHILD)
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string[]>
                {
                    ["Fan"] = ["rare_fan*1", "rare_fan*2", "rare_fan*3"],
                    ["Marquee"] = ["marquee*1", "marquee*2", "marquee*3"],
                    ["Wooden screen"] = ["wooden_screen*1", "wooden_screen*2", "wooden_screen*3"],
                    ["Other"] = ["throne", "spyro"],
                },
                o => o.WithStrictOrdering()
            );
        NamesOn(CHILD).Should().BeEmpty();
        _service.History.Undo.Should().ContainSingle();
    }

    [Fact]
    public async Task SubPages_OfFurniAllOfOneKind_PutThemOnThePageItself()
    {
        var ids = Furni(
            "xmas2014",
            "xmas14_elf1",
            "xmas14_hammock",
            "xmas14_palm",
            "xmas14_tikibar"
        );

        var added = await Adder.AddAsync(
            Editor,
            CHILD,
            Request(ids, AdminCatalogAddFurni.SORT_KIND),
            Ct
        );

        added.Value!.Done.Should().Be(4);
        PagesUnder(CHILD).Should().BeEmpty();
        NamesOn(CHILD)
            .Should()
            .Equal("xmas14_elf1", "xmas14_hammock", "xmas14_palm", "xmas14_tikibar");
    }

    [Fact]
    public async Task SubPages_PutAKindOnThePageOfItsTitleUnderItAlready()
    {
        _catalog.Db.Insert(Page(40, CHILD, "Marquee"));
        var ids = Furni(
            "rare",
            "marquee*1",
            "marquee*2",
            "marquee*3",
            "pillow*1",
            "pillow*2",
            "pillow*3"
        );

        var plan = await Adder.PlanAsync(CHILD, Request(ids, AdminCatalogAddFurni.SORT_KIND), Ct);

        plan.Value!.Pages.Select(x => (x.Title, x.PageId))
            .Should()
            .Equal(("Marquee", 40), ("Pillow", null));

        await Adder.AddAsync(Editor, CHILD, Request(ids, AdminCatalogAddFurni.SORT_KIND), Ct);

        PagesUnder(CHILD).Keys.Should().BeEquivalentTo("Marquee", "Pillow");
        NamesOn(40).Should().Equal("marquee*1", "marquee*2", "marquee*3");
    }

    [Fact]
    public async Task ByColour_PutsEachColourOnAPage_ReadFromTheNameOrThePartColours()
    {
        var ids = Coloured(
            "pura",
            ("chair*1", "Pink Chair", null),
            ("bed*1", "Pink Bed", null),
            ("lamp*1", "Pink Lamp", null),
            ("chair*2", "Aquamarine Chair", null),
            ("bed*2", "Turquoise Bed", null),
            ("chair*3", "Chair", ["#ffffff", "#99DCCC", "#ffffff", "#99DCCC"]),
            ("lamp*2", "Red Lamp", null)
        );

        var plan = await Adder.PlanAsync(CHILD, Request(ids, AdminCatalogAddFurni.SORT_COLOUR), Ct);

        plan.Value!.Pages.Select(x => (x.Title, x.Count, x.Pages.Length))
            .Should()
            .Equal(("Aqua", 3, 0), ("Pink", 3, 0), ("Other colours", 1, 0));

        await Adder.AddAsync(Editor, CHILD, Request(ids, AdminCatalogAddFurni.SORT_COLOUR), Ct);

        PagesUnder(CHILD)
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string[]>
                {
                    ["Aqua"] = ["chair*2", "bed*2", "chair*3"],
                    ["Pink"] = ["chair*1", "bed*1", "lamp*1"],
                    ["Other colours"] = ["lamp*2"],
                },
                o => o.WithStrictOrdering()
            );
    }

    [Fact]
    public async Task ByKindThenColour_SortsAKindByColour_OnlyWhereTwoColoursHaveThreeEach()
    {
        var ids = Coloured(
            "plasto",
            ("chair*1", "Pink Chair", null),
            ("chair*2", "Pink Chair", null),
            ("chair*3", "Pink Chair", null),
            ("chair*4", "Blue Chair", null),
            ("chair*5", "Blue Chair", null),
            ("chair*6", "Blue Chair", null),
            ("chair*7", "Red Chair", null),
            ("table*1", "Pink Table", null),
            ("table*2", "Pink Table", null),
            ("table*3", "Pink Table", null),
            ("table*4", "Blue Table", null),
            ("table*5", "Ice Cream Table", null)
        );
        var request = Request(ids, AdminCatalogAddFurni.SORT_KIND_COLOUR);

        var plan = await Adder.PlanAsync(CHILD, request, Ct);

        plan.Value!.Pages.Select(x => (x.Title, x.Count))
            .Should()
            .Equal(("Chair", 7), ("Table", 5));
        plan.Value.Pages[0]
            .Pages.Select(x => (x.Title, x.Count))
            .Should()
            .Equal(("Blue", 3), ("Pink", 3), ("Other colours", 1));
        plan.Value.Pages[1]
            .Pages.Should()
            .BeEmpty("only pink has three tables, and an ice cream is no cream colour");

        await Adder.AddAsync(Editor, CHILD, request, Ct);

        var chair = PageNamed(CHILD, "Chair");

        NamesOn(chair).Should().BeEmpty();
        PagesUnder(chair)
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string[]>
                {
                    ["Blue"] = ["chair*4", "chair*5", "chair*6"],
                    ["Pink"] = ["chair*1", "chair*2", "chair*3"],
                    ["Other colours"] = ["chair*7"],
                },
                o => o.WithStrictOrdering()
            );
        NamesOn(PageNamed(CHILD, "Table"))
            .Should()
            .Equal("table*1", "table*2", "table*3", "table*4", "table*5");
        _service.History.Undo.Should().ContainSingle();
    }

    [Fact]
    public async Task ASortItDoesNotKnow_IsRefused()
    {
        var ids = Furni("rare", "marquee*1");

        (await Adder.PlanAsync(CHILD, Request(ids, "size"), Ct)).Error.Should().Contain("kind");
    }

    /// <summary>An offer on a page, giving each of the definitions once, last on it.</summary>
    private void OfferOn(int offerId, int pageId, params int[] definitionIds)
    {
        _catalog.Db.Insert(
            new CatalogOfferEntity
            {
                Id = offerId,
                CatalogPageEntityId = pageId,
                LocalizationId = $"offer{offerId}",
                CostCredits = 3,
                CostCurrency = 0,
                ClubLevel = 0,
                CanGift = true,
                CanBundle = true,
                Visible = true,
                SortOrder = offerId,
                Page = null!,
            }
        );

        for (var i = 0; i < definitionIds.Length; i++)
            _catalog.Db.Insert(
                new CatalogProductEntity
                {
                    Id = offerId * 10 + i,
                    CatalogOfferEntityId = offerId,
                    ProductType = ProductType.Floor,
                    FurnitureDefinitionEntityId = definitionIds[i],
                    Quantity = 1,
                    Offer = null!,
                }
            );
    }

    private CatalogPageEntity PageAt(int id)
    {
        using var db = _catalog.Db.CreateDbContext();

        return db.CatalogPages.AsNoTracking().Single(x => x.Id == id);
    }

    /// <summary>A page under the furniture tab with a header, a teaser and texts, as a shop page has.</summary>
    private CatalogPageEntity DressedPage(int id, string title, string layout)
    {
        var page = Page(id, FURNITURE, title);

        page.Layout = layout;
        page.Icon = 7;
        page.ImageData = [$"{title}_header", $"{title}_teaser"];
        page.TextData = [$"{title}!", $"All about {title}."];
        _catalog.Db.Insert(page);

        return page;
    }

    private List<int> OfferIdsOn(int pageId)
    {
        using var db = _catalog.Db.CreateDbContext();

        return
        [
            .. db
                .CatalogOffers.Where(x => x.CatalogPageEntityId == pageId)
                .OrderBy(x => x.SortOrder)
                .Select(x => x.Id),
        ];
    }

    [Fact]
    public async Task Resorting_MovesAPagesOffersOntoAPageForEachKind_LeavingABundleOnIt()
    {
        const int RARES = 50;

        DressedPage(RARES, "Rares", "default_3x3_color_grouping");
        var ids = Furni(
            "rare",
            "marquee*1",
            "pillow*1",
            "marquee*2",
            "pillow*2",
            "marquee*3",
            "pillow*3"
        );

        for (var i = 0; i < ids.Length; i++)
            OfferOn(500 + i, RARES, ids[i]);

        OfferOn(510, RARES, ids[0], ids[1]);

        var plan = await Adder.PlanResortAsync(RARES, AdminCatalogAddFurni.SORT_KIND, Ct);

        plan.Value!.Pages.Select(x => (x.Title, x.PageId, x.Count))
            .Should()
            .Equal(("Marquee", null, 3), ("Pillow", null, 3));
        plan.Value.Staying.Should().Be(1, "the bundle sells two furni");

        var sorted = await Adder.ResortAsync(Editor, RARES, AdminCatalogAddFurni.SORT_KIND, Ct);

        sorted.Value!.Done.Should().Be(6);
        OfferIdsOn(PageNamed(RARES, "Marquee")).Should().Equal(500, 502, 504);
        OfferIdsOn(PageNamed(RARES, "Pillow")).Should().Equal(501, 503, 505);
        OfferIdsOn(RARES).Should().Equal(510);
        PageAt(PageNamed(RARES, "Marquee"))
            .Should()
            .BeEquivalentTo(
                new
                {
                    Layout = "default_3x3_color_grouping",
                    Icon = 7,
                    ImageData = new[] { "Rares_header", "Rares_teaser" },
                    TextData = new[] { "Rares!", "All about Rares." },
                },
                "a page sorted out of it takes its header, teaser and texts"
            );
        _service.History.Undo.Should().ContainSingle();
    }

    [Fact]
    public async Task PagesMadeAddingFurni_TakeAShopGridsHeaderTeaserAndTexts_NotAFrontPages()
    {
        DressedPage(52, "Rares", "default_3x3");
        DressedPage(53, "Welcome", "frontpage4");
        var rares = Furni(
            "rare",
            "marquee*1",
            "marquee*2",
            "marquee*3",
            "pillow*1",
            "pillow*2",
            "pillow*3"
        );
        var welcome = Furni("rare", "pillar*1", "pillar*2", "pillar*3", "fan*1", "fan*2", "fan*3");

        await Adder.AddAsync(Editor, 52, Request(rares, AdminCatalogAddFurni.SORT_KIND), Ct);
        await Adder.AddAsync(Editor, 53, Request(welcome, AdminCatalogAddFurni.SORT_KIND), Ct);

        var pillow = PageAt(PageNamed(52, "Pillow"));
        var fan = PageAt(PageNamed(53, "Fan"));

        (pillow.Layout, pillow.ImageData, pillow.TextData)
            .Should()
            .BeEquivalentTo(
                (
                    "default_3x3",
                    new[] { "Rares_header", "Rares_teaser" },
                    new[] { "Rares!", "All about Rares." }
                )
            );
        (fan.Layout, fan.Icon).Should().Be(("default_3x3", 7));
        (fan.ImageData ?? [])
            .Should()
            .BeEmpty("a front page's pictures mean something else on a shop grid");
        (fan.TextData ?? []).Should().BeEmpty();
    }

    [Fact]
    public async Task Resorting_APageItPartsNothingOf_IsRefused_AndChangesNothing()
    {
        const int XMAS = 51;

        _catalog.Db.Insert(Page(XMAS, FURNITURE, "Xmas 2014"));
        var ids = Furni("xmas2014", "xmas14_elf1", "xmas14_palm", "xmas14_tikibar");

        for (var i = 0; i < ids.Length; i++)
            OfferOn(520 + i, XMAS, ids[i]);

        var before = _catalog.Rows();

        var plan = await Adder.PlanResortAsync(XMAS, AdminCatalogAddFurni.SORT_KIND_COLOUR, Ct);

        plan.Value!.Pages.Should().BeEmpty();
        (await Adder.ResortAsync(Editor, XMAS, AdminCatalogAddFurni.SORT_KIND_COLOUR, Ct))
            .Error.Should()
            .Contain("Nothing to sort");
        _catalog.Rows().Should().Be(before);
    }

    [Fact]
    public async Task WithoutSubPages_EveryFurniGoesOnThePage()
    {
        var ids = Furni(
            "rare",
            "marquee*1",
            "marquee*2",
            "marquee*3",
            "pillow*1",
            "pillow*2",
            "pillow*3"
        );

        await Adder.AddAsync(Editor, CHILD, Request(ids, null), Ct);

        PagesUnder(CHILD).Should().BeEmpty();
        NamesOn(CHILD).Should().HaveCount(6);
    }
}
