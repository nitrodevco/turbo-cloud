using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Catalog;
using Turbo.Catalog.Editing;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// Taking Habbo's Builders Club catalogue in from a capture: the plan changes nothing and says
/// what would be added, changed and left out; applying makes the pages under the tab the Builders
/// Club opens on, with their offers in the capture's order, as one step to undo; taken in once,
/// the same capture has nothing left to do; a page here that has it is brought in line; pages
/// shown in the normal catalogue are never changed, and the Builders Club pages the capture lacks
/// stay unless asked to go.
/// </summary>
public sealed class AdminCatalogImportTests : IDisposable
{
    private static readonly PlayerId Editor = 1;

    private const int SOFA = 20;
    private const int FIRE = 21;
    private const int GOLD_BAR = 22;

    private readonly CatalogFixture _catalog = new();
    private readonly CatalogEditService _service;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AdminCatalogImportTests()
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

        _catalog.AddDefinition(SOFA, "bc_sofa");
        _catalog.AddDefinition(FIRE, "bc_fire");
        _catalog.AddDefinition(GOLD_BAR, "CF_50_goldbar");
    }

    public void Dispose() => _catalog.Dispose();

    private AdminCatalogImport Importer =>
        new(_catalog.Db, _service, new CapturingLogger<AdminCatalogImport>());

    private static CatalogCaptureOffer Offer(
        int id,
        string className,
        string type = "s",
        string extra = "",
        int credits = 3
    ) => new(id, className, credits, 0, 0, true, true, 0, [new(type, id, extra, 1, className)]);

    private static CatalogCaptureNode Node(
        int id,
        string name,
        string title,
        bool visible = true,
        params CatalogCaptureNode[] children
    ) => new(visible, 7, id, name, title, children);

    private static CatalogCapturePageEntry Page(int id, params CatalogCaptureOffer[] offers) =>
        new(new CatalogCapturePage(id, "default_3x3", new(["header"], ["text"]), offers));

    /// <summary>
    /// Habbo's tree as its client gets it: root, the one "furni" category, a page of blocks and
    /// a heading with a page of fire under it, and a hidden page. The blocks page lists its
    /// offers twice over, as a capture can.
    /// </summary>
    private static CatalogImportRequest Capture(
        bool takeOthersOut = false,
        string blocksTitle = "Building Blocks",
        int sofaCredits = 3
    )
    {
        var blocks = new[]
        {
            Offer(900, "bc_sofa", credits: sofaCredits),
            Offer(901, "poster", "i", "42"),
            Offer(902, "CF_50_goldbar"),
            Offer(903, "bc_unknown"),
        };

        return new(
            new CatalogCapture(
                new CatalogCaptureIndex(
                    Node(
                        -1,
                        "root",
                        "",
                        true,
                        Node(
                            2000,
                            "furni",
                            "Furni",
                            true,
                            Node(2001, "bc_blocks", blocksTitle),
                            Node(-1, "bc_heading", "Themes", true, Node(2003, "bc_fire", "Fire")),
                            Node(2004, "bc_secret", "Secret", visible: false)
                        )
                    )
                ),
                new Dictionary<string, CatalogCapturePageEntry>
                {
                    ["2000"] = Page(2000),
                    ["2001"] = Page(2001, [.. blocks, .. blocks]),
                    ["2003"] = Page(2003, Offer(904, "bc_fire")),
                    ["2004"] = Page(2004, Offer(905, "bc_sofa")),
                }
            ),
            takeOthersOut,
            Publish: false
        );
    }

    private string Rows(Func<string, bool>? where = null)
    {
        using var db = _catalog.Db.CreateDbContext();

        var lines = db
            .CatalogPages.AsNoTracking()
            .OrderBy(x => x.Id)
            .ToList()
            .Select(x =>
                $"page {x.Id} {x.ParentEntityId} {x.Localization} {x.Name} {x.Icon} {x.Layout} {x.SortOrder} {x.Display}"
            )
            .Concat(
                db.CatalogOffers.AsNoTracking()
                    .Include(x => x.Products)
                    .OrderBy(x => x.Id)
                    .ToList()
                    .Select(x =>
                        $"offer {x.Id} {x.CatalogPageEntityId} {x.LocalizationId} {x.CostCredits} {x.SortOrder} {string.Join(',', x.Products!.Select(p => $"{p.ProductType}:{p.FurnitureDefinitionEntityId}:{p.ExtraParam}"))}"
                    )
            );

        return string.Join('\n', where is null ? lines : lines.Where(where));
    }

    private static bool NormalPage(string row) =>
        row.StartsWith("page", StringComparison.Ordinal)
        && !row.Contains("BuildersClubOnly", StringComparison.Ordinal);

    [Fact]
    public async Task ThePlan_AddsTheCapturesPages_LeavesOutWhatItCannot_AndChangesNothing()
    {
        var before = Rows();

        var outcome = await Importer.PlanAsync(Capture(), Ct);

        outcome.Error.Should().BeNull();
        var plan = outcome.Value!;

        plan.Category.Should().Be("Furniture");
        plan.Pages.Select(x => $"{x.Path} {x.Action} {x.OffersAdded}")
            .Should()
            .Equal(
                "/furni/bc_blocks add 2",
                "/furni/bc_heading add 0",
                "/furni/bc_heading/bc_fire add 1"
            );
        plan.OffersAdded.Should().Be(3);
        plan.MissingClasses.Should().Equal("bc_unknown");
        plan.Skipped.Select(x => x.Reason)
            .Should()
            .BeEquivalentTo(
                "credit furni are never lent",
                "no furniture here is bc_unknown",
                "hidden in Habbo's Builders Club, and a page here is shown there or not in it"
            );
        plan.Skipped.Single(x => x.What == "/furni/bc_secret").Offers.Should().Be(1);
        plan.Changes.Should().BeTrue();
        plan.Saved.Should().BeFalse();
        Rows().Should().Be(before);
    }

    [Fact]
    public async Task Applying_MakesTheBuildersClubTree_UnderItsTab_AsOneStep_AndTheNormalCatalogIsAsItWas()
    {
        var normal = Rows(NormalPage);

        var outcome = await Importer.ApplyAsync(Editor, Capture(), Ct);

        outcome.Error.Should().BeNull();
        outcome.Value!.Saved.Should().BeTrue();
        _service.History.Undo.Should().ContainSingle();

        await using var db = await _catalog.Db.CreateDbContextAsync(Ct);
        var blocks = await db.CatalogPages.SingleAsync(x => x.Name == "bc_blocks", Ct);
        var heading = await db.CatalogPages.SingleAsync(x => x.Name == "bc_heading", Ct);
        var fire = await db.CatalogPages.SingleAsync(x => x.Name == "bc_fire", Ct);

        blocks.ParentEntityId.Should().Be(FURNITURE);
        blocks.Display.Should().Be(CatalogPageDisplay.BuildersClubOnly);
        blocks.Localization.Should().Be("Building Blocks");
        blocks.ImageData.Should().Equal("header");
        heading.ParentEntityId.Should().Be(FURNITURE);
        fire.ParentEntityId.Should().Be(heading.Id);
        (await db.CatalogPages.AnyAsync(x => x.Name == "bc_secret", Ct)).Should().BeFalse();

        var onBlocks = await db
            .CatalogOffers.Where(x => x.CatalogPageEntityId == blocks.Id)
            .OrderBy(x => x.SortOrder)
            .Select(x => x.Products!.Single())
            .ToListAsync(Ct);
        onBlocks
            .Select(x => $"{x.ProductType}:{x.FurnitureDefinitionEntityId}:{x.ExtraParam}")
            .Should()
            .Equal($"Floor:{SOFA}:", $"Wall:{POSTER}:42");

        Rows(NormalPage).Should().Be(normal);
    }

    [Fact]
    public async Task TakenInOnce_TheSameCapture_HasNothingLeftToDo()
    {
        (await Importer.ApplyAsync(Editor, Capture(), Ct)).Error.Should().BeNull();
        var after = Rows();

        var first = (await Importer.PlanAsync(Capture(), Ct)).Value!;
        var second = (await Importer.PlanAsync(Capture(), Ct)).Value!;
        var again = (await Importer.ApplyAsync(Editor, Capture(), Ct)).Value!;

        foreach (var plan in new[] { first, second, again })
        {
            plan.Changes.Should().BeFalse();
            plan.OffersAdded.Should().Be(0);
            plan.OffersUnchanged.Should().Be(3);
            plan.Pages.Should().OnlyContain(x => x.Action == CatalogImportPageChange.UNCHANGED);
        }

        again.Saved.Should().BeFalse();
        _service.History.Undo.Should().ContainSingle();
        Rows().Should().Be(after);
    }

    [Fact]
    public async Task APageHereThatTheCaptureHas_IsBroughtInLine_TitleAndPrice()
    {
        (await Importer.ApplyAsync(Editor, Capture(), Ct)).Error.Should().BeNull();

        var plan = (
            await Importer.PlanAsync(Capture(blocksTitle: "Blocks", sofaCredits: 9), Ct)
        ).Value!;

        var blocks = plan.Pages.Single(x => x.Name == "bc_blocks");
        blocks.Action.Should().Be(CatalogImportPageChange.UPDATE);
        blocks.Fields.Should().Equal("title");
        blocks.OffersChanged.Should().Be(1);
        plan.OffersAdded.Should().Be(0);

        (await Importer.ApplyAsync(Editor, Capture(blocksTitle: "Blocks", sofaCredits: 9), Ct))
            .Error.Should()
            .BeNull();

        await using var db = await _catalog.Db.CreateDbContextAsync(Ct);
        var page = await db.CatalogPages.SingleAsync(x => x.Name == "bc_blocks", Ct);
        page.Localization.Should().Be("Blocks");
        (
            await db
                .CatalogOffers.Where(x => x.CatalogPageEntityId == page.Id)
                .Select(x => new
                {
                    x.CostCredits,
                    Definition = x.Products!.Single().FurnitureDefinitionEntityId,
                })
                .ToListAsync(Ct)
        )
            .Single(x => x.Definition == SOFA)
            .CostCredits.Should()
            .Be(9);
        (await Importer.PlanAsync(Capture(blocksTitle: "Blocks", sofaCredits: 9), Ct))
            .Value!.Changes.Should()
            .BeFalse();
    }

    [Fact]
    public async Task TheBuildersClubPagesTheCaptureLacks_StayUnlessAskedToGo()
    {
        (await Importer.ApplyAsync(Editor, Capture(), Ct)).Error.Should().BeNull();

        await using (var db = await _catalog.Db.CreateDbContextAsync(Ct))
            (await db.CatalogPages.SingleAsync(x => x.Id == BUILDERS_PAGE, Ct))
                .Display.Should()
                .Be(CatalogPageDisplay.BuildersClubOnly);

        var plan = (await Importer.PlanAsync(Capture(takeOthersOut: true), Ct)).Value!;
        plan.PagesTakenOut.Select(x => $"{x.PageId} {x.Action} {string.Join(',', x.Fields)}")
            .Should()
            .Equal($"{BUILDERS_PAGE} out invisible");

        (await Importer.ApplyAsync(Editor, Capture(takeOthersOut: true), Ct))
            .Error.Should()
            .BeNull();

        await using (var db = await _catalog.Db.CreateDbContextAsync(Ct))
            (await db.CatalogPages.SingleAsync(x => x.Id == BUILDERS_PAGE, Ct))
                .Display.Should()
                .Be(CatalogPageDisplay.Invisible);
    }

    [Fact]
    public async Task NoIndex_IsRefusedWithWhy()
    {
        var outcome = await Importer.PlanAsync(new CatalogImportRequest(null, false, false), Ct);

        outcome.Value.Should().BeNull();
        outcome.Error.Should().Contain("no Builders Club index");
    }
}
