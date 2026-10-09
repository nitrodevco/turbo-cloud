using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Catalog;
using Turbo.Admin.Configuration;
using Turbo.Catalog.Editing;
using Turbo.Database.Entities.Catalog;
using Turbo.Inventory;
using Turbo.Inventory.Configuration;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// Generating a whole catalog: tabs in the order players meet them, furni by line with a family
/// of lines in one folder, wired by kind, rares hidden until priced, the special pages built as
/// their builders build them; in replace mode the old tabs go into one hidden tab and the offers
/// there now are moved onto the new pages, and the lot is one step to undo.
/// </summary>
public sealed class AdminCatalogGeneratorTests : IDisposable
{
    private static readonly PlayerId Editor = 1;

    private readonly CatalogFixture _catalog = new();
    private readonly CatalogEditService _service;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AdminCatalogGeneratorTests()
    {
        _service = new CatalogEditService(
            _catalog.Db,
            _catalog.Definitions,
            _catalog.NormalProvider(),
            _catalog.BuildersClubProvider(),
            _catalog.Fakes.Create<ISessionGateway>(),
            _catalog.Fakes.Create<IGrainFactory>(),
            new CapturingLogger<ICatalogEditService>()
        );

        _catalog.AddDefinition(
            30,
            "pura_chair",
            set: x => (x.FurniLine, x.ClientCategory) = ("pura", "chair")
        );
        _catalog.AddDefinition(
            31,
            "pura_table",
            set: x => (x.FurniLine, x.ClientCategory) = ("pura", "table")
        );
        _catalog.AddDefinition(32, "iced_chair", set: x => x.FurniLine = "iced");
        _catalog.AddDefinition(33, "iced_sofa*1", set: x => x.FurniLine = "iced_dark");
        _catalog.AddDefinition(34, "wf_trg_walks_on", set: x => x.FurniLine = "wired");
        _catalog.AddDefinition(35, "wf_act_toggle", set: x => x.FurniLine = "wired");
        _catalog.AddDefinition(36, "rare_dragon", set: x => x.FurniLine = "rare");
        _catalog.AddDefinition(37, "hc_gift_chair", set: x => x.FurniLine = "habbo_club_gifts");
        _catalog.AddDefinition(39, "xmas_tree18", set: x => x.FurniLine = "xmas2018");
        _catalog.AddDefinition(40, "xmas_tree19", set: x => x.FurniLine = "xmas2019");
        _catalog.AddDefinition(38, "cup_gold", set: x => x.Logic = TrophyData.LOGIC_NAME);
    }

    public void Dispose() => _catalog.Dispose();

    private AdminCatalogBuilder Builder =>
        new(
            _catalog.Db,
            _service,
            new GivableEffects(Options.Create(new EffectConfig())),
            Options.Create(new AdminConfig())
        );

    private static CatalogGenerateRequest Request(
        string mode = AdminCatalogBuilder.MODE_REPLACE,
        CatalogGeneratePageEdit[]? edits = null
    ) =>
        new(
            mode,
            null,
            ReuseOffers: true,
            MaxPerPage: 100,
            CostCredits: 3,
            CostCurrency: 0,
            CurrencyTypeId: null,
            edits
        );

    private static IEnumerable<CatalogGeneratedPage> All(IEnumerable<CatalogGeneratedPage> pages) =>
        pages.SelectMany(x => All(x.Children).Prepend(x));

    private string Snapshot()
    {
        using var db = _catalog.Db.CreateDbContext();

        return string.Join(
            '\n',
            db.CatalogPages.AsNoTracking()
                .OrderBy(x => x.Id)
                .Select(x =>
                    $"page {x.Id} {x.ParentEntityId} {x.Localization} {x.Name} {x.SortOrder} {x.Display}"
                )
                .ToList()
                .Concat(
                    db.CatalogOffers.AsNoTracking()
                        .OrderBy(x => x.Id)
                        .Select(x => $"offer {x.Id} {x.CatalogPageEntityId} {x.SortOrder}")
                        .ToList()
                )
                .Concat(
                    db.CatalogProducts.AsNoTracking()
                        .OrderBy(x => x.Id)
                        .Select(x =>
                            $"product {x.Id} {x.CatalogOfferEntityId} {x.FurnitureDefinitionEntityId}"
                        )
                        .ToList()
                )
        );
    }

    [Fact]
    public async Task ThePlan_HasTheTabsInOrder_TheFurniByThemeAndLine_AndAFamilyOfLinesInOneFolder()
    {
        var outcome = await Builder.PlanCatalogAsync(Request(), Ct);

        outcome.Error.Should().BeNull();
        var plan = outcome.Value!;

        plan.Tabs.Select(x => x.Title)
            .Should()
            .Equal(
                "Front Page",
                "Habbo Club",
                "Furni",
                "Wired",
                "Rares",
                "Groups",
                "Builders Club"
            );
        plan.ArchivedTabs.Should().Be(2);

        var furni = plan.Tabs.Single(x => x.Section == AdminCatalogBuilder.SECTION_FURNI);

        furni
            .Children.Select(x => x.Title)
            .Should()
            .Equal("Trophies", "Seasonal", "Classic lines", "More furni");

        var classic = furni.Children.Single(x => x.Title == "Classic lines");

        classic
            .Children.Select(x => (x.Title, x.Layout))
            .Should()
            .Equal(
                ("Iced", "default_3x3"),
                ("Iced Dark", "default_3x3_color_grouping"),
                ("Pura", "default_3x3")
            );
        classic.Children.Single(x => x.Title == "Pura").NewOffers.Should().Be(2);
        furni
            .Children.Single(x => x.Title == "Seasonal")
            .Children.Should()
            .ContainSingle()
            .Which.Should()
            .Match<CatalogGeneratedPage>(x =>
                x.Title == "Christmas"
                && x.Children.Select(c => c.Title)
                    .SequenceEqual(new[] { "Christmas 2018", "Christmas 2019" })
            );
        furni.Children.Single(x => x.Title == "Trophies").Layout.Should().Be("trophies");

        var wired = plan.Tabs.Single(x => x.Section == AdminCatalogBuilder.SECTION_WIRED);

        wired.Children.Select(x => x.Title).Should().Equal("Triggers", "Effects");

        var rares = All(plan.Tabs.Where(x => x.Section == AdminCatalogBuilder.SECTION_RARES))
            .Single(x => x.Title == "Rare");

        rares.Display.Should().Be("invisible", "a rare made new has the everyday price");
        All(plan.Tabs)
            .Should()
            .NotContain(x =>
                x.Title.Contains("Club Gifts", StringComparison.OrdinalIgnoreCase)
                && x.NewOffers > 0
            );
        plan.Tabs.Single(x => x.Section == AdminCatalogBuilder.SECTION_CLUB)
            .Name.Should()
            .Be("hc_membership");
    }

    [Fact]
    public async Task ThePlan_MovesTheOffersThereNow_RatherThanSellTheFurniTwice()
    {
        var plan = (await Builder.PlanCatalogAsync(Request(), Ct)).Value!;

        var more = All(plan.Tabs).Single(x => x.Key == "furni/more/kind:other");

        more.MovedOffers.Should()
            .Be(1, "the first offer of the chair moves; the rest stay in the old tabs");
        plan.MovedOffers.Should().Be(1);
    }

    [Fact]
    public async Task EditsToThePlan_RetitleReIconAndLeaveOutPages()
    {
        var plan = (
            await Builder.PlanCatalogAsync(
                Request(
                    edits:
                    [
                        new("furni/theme:classic/line:pura", "Pura Range", 77, false),
                        new(AdminCatalogBuilder.SECTION_WIRED, null, null, true),
                    ]
                ),
                Ct
            )
        ).Value!;

        All(plan.Tabs)
            .Single(x => x.Key == "furni/theme:classic/line:pura")
            .Should()
            .Match<CatalogGeneratedPage>(x => x.Title == "Pura Range" && x.Icon == 77);
        plan.Tabs.Should().NotContain(x => x.Section == AdminCatalogBuilder.SECTION_WIRED);
    }

    [Fact]
    public async Task AThemeOfManyLines_IsSplitByLetter_SoNoFolderListsMoreThanTwentyFour()
    {
        for (var i = 0; i < 30; i++)
        {
            var line = $"{(char)('a' + i % 26)}{(char)('a' + i / 26)}line";

            _catalog.AddDefinition(100 + i, $"{line}_chair", set: x => x.FurniLine = line);
        }

        var plan = (await Builder.PlanCatalogAsync(Request(), Ct)).Value!;
        var more = All(plan.Tabs).Single(x => x.Key == "furni/theme:other");

        more.Children.Select(x => x.Title)
            .Should()
            .Equal("More lines A\u2013K", "More lines L\u2013Z");
        more.Children.Should().AllSatisfy(x => x.Children.Length.Should().BeLessThanOrEqualTo(24));
        more.Children.Sum(x => x.Children.Length).Should().Be(30);
    }

    [Fact]
    public async Task ALineOverThreePagesOrMore_IsAFolderOfItsOwn()
    {
        for (var i = 0; i < 50; i++)
            _catalog.AddDefinition(
                200 + i,
                $"plasto_chair_{i:D2}",
                set: x => x.FurniLine = "plasto"
            );

        var plan = (await Builder.PlanCatalogAsync(Request() with { MaxPerPage = 20 }, Ct)).Value!;
        var plasto = All(plan.Tabs).Single(x => x.Key == "furni/theme:classic/line:plasto");

        plasto
            .Children.Select(x => (x.Title, x.NewOffers))
            .Should()
            .Equal(("Plasto 1", 20), ("Plasto 2", 20), ("Plasto 3", 10));
    }

    [Fact]
    public async Task Generating_PutsTheOldTabsAway_MakesTheNewOnes_AndIsOneStepToUndo()
    {
        var before = Snapshot();

        var outcome = await Builder.GenerateCatalogAsync(Editor, Request(), Ct);

        outcome.Error.Should().BeNull();
        outcome.Value!.PagesMoved.Should().Be(2);

        using (var db = _catalog.Db.CreateDbContext())
        {
            var tabs = db
                .CatalogPages.Where(x => x.ParentEntityId == ROOT)
                .OrderBy(x => x.SortOrder)
                .ToList();

            tabs.Select(x => x.Localization)
                .Should()
                .Equal(
                    "Front Page",
                    "Habbo Club",
                    "Furni",
                    "Wired",
                    "Rares",
                    "Groups",
                    "Builders Club",
                    "Old catalog"
                );
            var archive = tabs[^1];

            archive.Display.Should().Be(CatalogPageDisplay.Invisible);
            db.CatalogPages.Where(x => x.ParentEntityId == archive.Id)
                .Select(x => x.Id)
                .Should()
                .BeEquivalentTo([FURNITURE, HIDDEN_PAGE]);
            var pura = db.CatalogPages.Single(x => x.Localization == "Pura");

            db.CatalogPages.Single(x => x.Id == pura.ParentEntityId)
                .Localization.Should()
                .Be("Classic lines");

            db.CatalogOffers.Where(x => x.CatalogPageEntityId == pura.Id)
                .Select(x => x.LocalizationId)
                .Should()
                .Equal("pura_chair", "pura_table");
            db.CatalogOffers.Single(x => x.Id == SOLD)
                .CatalogPageEntityId.Should()
                .NotBe(FURNITURE);
        }

        _service
            .History.Undo.Should()
            .ContainSingle()
            .Which.Label.Should()
            .Be("generated a fresh catalog");

        (await _service.UndoAsync(Editor, Ct)).Error.Should().BeNull();

        Snapshot().Should().Be(before);
    }

    [Fact]
    public async Task Alongside_TheNewTabsComeHidden_AndNothingThereNowMoves()
    {
        var plan = (
            await Builder.PlanCatalogAsync(Request(AdminCatalogBuilder.MODE_ALONGSIDE), Ct)
        ).Value!;

        plan.ArchivedTabs.Should().Be(0);
        plan.MovedOffers.Should().Be(0);
        plan.Tabs.Should().OnlyContain(x => x.Display == "invisible");
    }
}
