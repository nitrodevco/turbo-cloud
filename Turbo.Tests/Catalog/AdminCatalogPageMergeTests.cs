using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Admin.Catalog;
using Turbo.Catalog.Editing;
using Turbo.Database.Entities.Catalog;
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
/// Pages of one name, as two imports made apart leave them: listed by the duplicates audit, and
/// merged into one as one step to undo - the others' offers moved to it, those it gives already
/// deleted, the pages under them moved under it, and the page kept shown in each catalogue one of
/// them was.
/// </summary>
public sealed class AdminCatalogPageMergeTests : IDisposable
{
    private static readonly PlayerId Editor = 1;

    private const int NORMAL_LODGE = 60;
    private const int BUILDERS_LODGE = 61;
    private const int EXTRAS = 62;

    private const int LODGE_CHAIR = 600;
    private const int LODGE_POSTER = 601;
    private const int COPY_CHAIR = 610;
    private const int COPY_PET = 611;

    private readonly CatalogFixture _catalog = new();
    private readonly CatalogEditService _service;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AdminCatalogPageMergeTests()
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

        // The normal catalogue's import's page, and the Builders Club's copy of it, with its own
        // page under it.
        _catalog.Db.Insert(Page(NORMAL_LODGE, FURNITURE, "Lodge", sort: 5));
        _catalog.Db.Insert(
            Page(BUILDERS_LODGE, FURNITURE, "Lodge", CatalogPageDisplay.BuildersClubOnly, 6)
        );
        _catalog.Db.Insert(
            Page(EXTRAS, BUILDERS_LODGE, "Extras", CatalogPageDisplay.BuildersClubOnly)
        );
        Offer(LODGE_CHAIR, NORMAL_LODGE, ProductType.Floor, CHAIR);
        Offer(LODGE_POSTER, NORMAL_LODGE, ProductType.Wall, POSTER);
        Offer(COPY_CHAIR, BUILDERS_LODGE, ProductType.Floor, CHAIR);
        Offer(COPY_PET, BUILDERS_LODGE, ProductType.Floor, PET_ITEM);
    }

    public void Dispose() => _catalog.Dispose();

    private AdminCatalogPageMerge Merger =>
        new(_catalog.Db, _service, new CapturingLogger<AdminCatalogPageMerge>());

    private void Offer(int id, int pageId, ProductType type, int definition)
    {
        _catalog.Db.Insert(
            new CatalogOfferEntity
            {
                Id = id,
                CatalogPageEntityId = pageId,
                LocalizationId = $"offer{id}",
                CostCredits = 3,
                CostCurrency = 0,
                ClubLevel = 0,
                CanGift = true,
                CanBundle = true,
                Visible = true,
                SortOrder = id,
                Page = null!,
            }
        );
        _catalog.Db.Insert(
            new CatalogProductEntity
            {
                Id = id,
                CatalogOfferEntityId = id,
                ProductType = type,
                FurnitureDefinitionEntityId = definition,
                Quantity = 1,
                Offer = null!,
            }
        );
    }

    [Fact]
    public async Task TheAudit_ListsThePagesThatShareAName()
    {
        var duplicates = await new AdminCatalogAudit(_catalog.Db).GetDuplicatePagesAsync(Ct);

        var lodge = duplicates.Items.Single(x => x.Name == "lodge");

        lodge
            .Pages.Select(x => (x.PageId, x.Display, x.Offers, x.Pages))
            .Should()
            .Equal((NORMAL_LODGE, "regular", 2, 0), (BUILDERS_LODGE, "bc_only", 2, 1));
        lodge.Pages[0].Path.Should().Be("Furniture / Lodge");
    }

    [Fact]
    public async Task Merging_MakesOnePageShownInBoth_WithEverythingTheOtherHeld_AsOneStep()
    {
        var before = _catalog.Rows();

        var merged = await Merger.MergeAsync(Editor, NORMAL_LODGE, [BUILDERS_LODGE], Ct);

        merged.Error.Should().BeNull();
        merged
            .Value.Should()
            .BeEquivalentTo(
                new
                {
                    OffersMoved = 1,
                    OffersDropped = 1,
                    PagesMoved = 1,
                    PagesDeleted = 1,
                    Display = "both",
                }
            );

        await using (var db = await _catalog.Db.CreateDbContextAsync(Ct))
        {
            (await db.CatalogPages.SingleAsync(x => x.Id == NORMAL_LODGE, Ct))
                .Display.Should()
                .Be(CatalogPageDisplay.Both);
            (await db.CatalogPages.AnyAsync(x => x.Id == BUILDERS_LODGE, Ct)).Should().BeFalse();
            (await db.CatalogPages.SingleAsync(x => x.Id == EXTRAS, Ct))
                .ParentEntityId.Should()
                .Be(NORMAL_LODGE);
            (
                await db
                    .CatalogOffers.Where(x => x.CatalogPageEntityId == NORMAL_LODGE)
                    .OrderBy(x => x.SortOrder)
                    .Select(x => x.Id)
                    .ToListAsync(Ct)
            )
                .Should()
                .Equal([LODGE_CHAIR, LODGE_POSTER, COPY_PET], "the copy's chair was given already");
        }

        _service.History.Undo.Select(x => x.Label).Should().Equal("merged the pages lodge");
        (await _service.UndoAsync(Editor, Ct)).Error.Should().BeNull();
        _catalog.Rows().Should().Be(before);
    }

    [Fact]
    public async Task APage_IsNotMergedIntoOneUnderIt_NorPagesOfAnotherName()
    {
        var before = _catalog.Rows();

        _catalog.Db.Insert(Page(63, EXTRAS, "Lodge", CatalogPageDisplay.BuildersClubOnly));

        (await Merger.MergeAsync(Editor, 63, [BUILDERS_LODGE], Ct))
            .Error.Should()
            .Contain("under it");
        (await Merger.MergeAsync(Editor, NORMAL_LODGE, [EXTRAS], Ct))
            .Error.Should()
            .Contain("one name");
        _service.History.Undo.Should().BeEmpty();
    }
}
