using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Catalog;
using Turbo.Catalog.Editing;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// Furni the audit lists, put on a page in one go: an offer each on the page, or sorted onto a
/// page under it for each kind - the first word of the class name, the line's own name passed
/// over - with the kinds too small for a page sharing one; furni all of one kind stay on the page.
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

    private static CatalogAddFurniRequest Request(int[] ids, bool subPages) =>
        new(ids, 3, 0, null, 0, CanGift: true, Visible: true, subPages);

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

        var plan = await Adder.PlanAsync(CHILD, Request(ids, subPages: true), Ct);

        plan.Value!.Pages.Select(x => (x.Title, x.PageId, x.Count))
            .Should()
            .Equal(
                ("Fan", null, 3),
                ("Marquee", null, 3),
                ("Wooden screen", null, 3),
                ("Other", null, 2)
            );

        var added = await Adder.AddAsync(Editor, CHILD, Request(ids, subPages: true), Ct);

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

        var added = await Adder.AddAsync(Editor, CHILD, Request(ids, subPages: true), Ct);

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

        var plan = await Adder.PlanAsync(CHILD, Request(ids, subPages: true), Ct);

        plan.Value!.Pages.Select(x => (x.Title, x.PageId))
            .Should()
            .Equal(("Marquee", 40), ("Pillow", null));

        await Adder.AddAsync(Editor, CHILD, Request(ids, subPages: true), Ct);

        PagesUnder(CHILD).Keys.Should().BeEquivalentTo("Marquee", "Pillow");
        NamesOn(40).Should().Equal("marquee*1", "marquee*2", "marquee*3");
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

        await Adder.AddAsync(Editor, CHILD, Request(ids, subPages: false), Ct);

        PagesUnder(CHILD).Should().BeEmpty();
        NamesOn(CHILD).Should().HaveCount(6);
    }
}
