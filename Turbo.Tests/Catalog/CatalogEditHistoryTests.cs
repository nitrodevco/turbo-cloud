using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Orleans;
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
/// The catalog editor's history: every edit since the last publish can be undone, exactly - ids,
/// order and what an offer gives included - and done again; the lot can be thrown away, leaving
/// the catalog players have; and an undo is refused rather than trample what changed since or
/// take away what now leans on a row. Many edits made as one (a tree) are one step.
/// </summary>
public sealed class CatalogEditHistoryTests : IDisposable
{
    private static readonly PlayerId Editor = 1;

    private readonly CatalogFixture _catalog = new();
    private readonly CatalogEditService _service;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public CatalogEditHistoryTests()
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

    private string Snapshot() => _catalog.Rows();

    private CatalogPageEntity PageRow(int id)
    {
        using var db = _catalog.Db.CreateDbContext();

        return db.CatalogPages.AsNoTracking().Single(x => x.Id == id);
    }

    private bool OfferExists(int id)
    {
        using var db = _catalog.Db.CreateDbContext();

        return db.CatalogOffers.Any(x => x.Id == id);
    }

    private static CatalogOfferDraft ChairOffer(int pageId, int credits = 4) =>
        new(
            pageId,
            string.Empty,
            credits,
            0,
            null,
            true,
            true,
            0,
            true,
            new CatalogProductDraft(ProductType.Floor, CHAIR, null, 1)
        );

    [Fact]
    public async Task UndoingAMove_PutsThePageBack_AndRedoMovesItAgain()
    {
        var before = Snapshot();

        (await _service.MovePageAsync(Editor, CHILD, HIDDEN_PAGE, 0, Ct)).Saved.Should().BeTrue();

        var undone = await _service.UndoAsync(Editor, Ct);

        undone.Saved.Should().BeTrue();
        Snapshot().Should().Be(before);
        _service.UnpublishedChanges.Should().Be(0);
        _service.History.Undo.Should().BeEmpty();
        _service.History.Redo.Select(x => x.Label).Should().Equal("moved the page Chairs");

        (await _service.RedoAsync(Editor, Ct)).Saved.Should().BeTrue();

        PageRow(CHILD).ParentEntityId.Should().Be(HIDDEN_PAGE);
        _service.UnpublishedChanges.Should().Be(1);
        _service.History.Redo.Should().BeEmpty();
    }

    [Fact]
    public async Task UndoingADeletedOffer_PutsItBackWithWhatItGave_UnderTheSameIds()
    {
        var before = Snapshot();

        (await _service.DeleteOfferAsync(Editor, SOLD, Ct)).Saved.Should().BeTrue();
        OfferExists(SOLD).Should().BeFalse();

        (await _service.UndoAsync(Editor, Ct)).Error.Should().BeNull();

        Snapshot().Should().Be(before);
    }

    [Fact]
    public async Task UndoingANewOffer_TakesItAway_AndRedoBringsItBackAsItWas()
    {
        var before = Snapshot();
        var created = await _service.CreateOfferAsync(Editor, ChairOffer(CHILD), Ct);
        var afterCreate = Snapshot();

        (await _service.UndoAsync(Editor, Ct)).Error.Should().BeNull();
        Snapshot().Should().Be(before);

        (await _service.RedoAsync(Editor, Ct)).Saved.Should().BeTrue();
        Snapshot().Should().Be(afterCreate);
        OfferExists(created.Id).Should().BeTrue();
    }

    [Fact]
    public async Task Discarding_PutsBackTheCatalogPlayersHave_AfterEditsOfEveryKind()
    {
        var before = Snapshot();

        await _service.UpdatePageAsync(
            Editor,
            CHILD,
            new CatalogPageDraft(
                "Seats",
                "seats",
                7,
                "default_3x3",
                ["banner"],
                ["Sit"],
                CatalogPageDisplay.Regular
            ),
            Ct
        );
        await _service.CreateOfferAsync(Editor, ChairOffer(CHILD), Ct);
        await _service.MoveOfferAsync(Editor, IN_DUCKETS, CHILD, 0, Ct);
        await _service.DeleteOfferAsync(Editor, IN_DIAMONDS, Ct);
        await _service.SaveFeaturedItemsAsync(
            Editor,
            [
                new CatalogFeaturedItemDraft(
                    "Seats",
                    "x.png",
                    CatalogFrontPageItemType.Page,
                    "seats",
                    null
                ),
            ],
            Ct
        );
        await _service.MovePageAsync(Editor, CHILD, ROOT, 0, Ct);

        Snapshot().Should().NotBe(before);
        _service.History.Undo.Should().HaveCount(6);

        var discarded = await _service.DiscardAsync(Editor, Ct);

        discarded.Saved.Should().BeTrue();
        discarded.Id.Should().Be(6);
        Snapshot().Should().Be(before);
        _service.UnpublishedChanges.Should().Be(0);
        _service.History.Redo.Should().HaveCount(6, "thrown away, they can still be done again");
    }

    [Fact]
    public async Task Publishing_ForgetsTheHistory()
    {
        await _service.MovePageAsync(Editor, CHILD, HIDDEN_PAGE, 0, Ct);
        await _service.PublishAsync(Editor, Ct);

        var undone = await _service.UndoAsync(Editor, Ct);

        undone.Saved.Should().BeFalse();
        undone.Error.Should().Be("There is nothing to undo.");
        PageRow(CHILD).ParentEntityId.Should().Be(HIDDEN_PAGE);
    }

    [Fact]
    public async Task Undoing_IsRefused_WhenTheRowWasChangedSince()
    {
        await _service.UpdatePageAsync(
            Editor,
            CHILD,
            new CatalogPageDraft(
                "Seats",
                "chairs",
                1,
                "default_3x3",
                [],
                [],
                CatalogPageDisplay.Regular
            ),
            Ct
        );

        using (var db = _catalog.Db.CreateDbContext())
        {
            db.CatalogPages.Single(x => x.Id == CHILD).Icon = 99;
            db.SaveChanges();
        }

        var undone = await _service.UndoAsync(Editor, Ct);

        undone.Saved.Should().BeFalse();
        undone.Error.Should().Contain("Seats").And.Contain("changed since");
        PageRow(CHILD).Localization.Should().Be("Seats");
        _service.History.Undo.Should().HaveCount(1, "a refused undo stays to be tried again");
    }

    [Fact]
    public async Task Undoing_IsRefused_WhenItWouldTakeAwayAnOfferThatNowSellsALimitedSeries()
    {
        var created = await _service.CreateOfferAsync(Editor, ChairOffer(CHILD), Ct);

        using (var db = _catalog.Db.CreateDbContext())
        {
            var product = db.CatalogProducts.Single(x => x.CatalogOfferEntityId == created.Id);

            db.LtdSeries.Add(
                new LtdSeriesEntity
                {
                    CatalogProductEntityId = product.Id,
                    TotalQuantity = 10,
                    RemainingQuantity = 10,
                    RaffleWindowSeconds = 30,
                    IsActive = true,
                }
            );
            db.SaveChanges();
        }

        var undone = await _service.UndoAsync(Editor, Ct);

        undone.Saved.Should().BeFalse();
        undone.Error.Should().Contain("limited series");
        OfferExists(created.Id).Should().BeTrue();
    }

    [Fact]
    public async Task EditsInAGroup_AreOneStep()
    {
        var before = Snapshot();

        await _service.GroupAsync(
            Editor,
            "filled the chairs",
            async () =>
            {
                await _service.CreateOfferAsync(Editor, ChairOffer(CHILD, 1), Ct);
                await _service.CreateOfferAsync(Editor, ChairOffer(CHILD, 2), Ct);

                return 0;
            }
        );

        _service
            .History.Undo.Should()
            .ContainSingle()
            .Which.Should()
            .Match<CatalogHistoryEntry>(x => x.Label == "filled the chairs" && x.Edits == 2);
        _service.UnpublishedChanges.Should().Be(2);

        (await _service.UndoAsync(Editor, Ct)).Error.Should().BeNull();

        Snapshot().Should().Be(before);
        _service.UnpublishedChanges.Should().Be(0);
    }

    [Fact]
    public async Task ATree_MakesPagesUnderEachOther_WithTheirOffersInOrder_AsOneStep()
    {
        var before = Snapshot();
        var draft = new CatalogTreeDraft(
            [
                new CatalogNewPage(
                    CatalogPageRef.Saved(ROOT),
                    new CatalogPageDraft(
                        "Shop",
                        null,
                        2,
                        "default_3x3",
                        [],
                        [],
                        CatalogPageDisplay.Regular
                    )
                ),
                new CatalogNewPage(
                    CatalogPageRef.Made(0),
                    new CatalogPageDraft(
                        "Seats",
                        "seats",
                        14,
                        "default_3x3",
                        [],
                        [],
                        CatalogPageDisplay.Regular
                    )
                ),
            ],
            [
                new CatalogNewOffer(CatalogPageRef.Made(1), ChairOffer(0, 1)),
                new CatalogNewOffer(CatalogPageRef.Made(1), ChairOffer(0, 2)),
            ],
            [new CatalogOfferPlacement(SOLD, CatalogPageRef.Made(1))],
            [new CatalogPagePlacement(HIDDEN_PAGE, CatalogPageRef.Made(0))]
        );

        var result = await _service.BuildTreeAsync(Editor, "made a shop", draft, Ct);

        result.Error.Should().BeNull();
        result.PageIds.Should().HaveCount(2);

        using (var db = _catalog.Db.CreateDbContext())
        {
            var shop = db.CatalogPages.Single(x => x.Id == result.PageIds[0]);
            var seats = db.CatalogPages.Single(x => x.Id == result.PageIds[1]);

            shop.ParentEntityId.Should().Be(ROOT);
            shop.SortOrder.Should().Be(2, "after the tabs there");
            seats.ParentEntityId.Should().Be(shop.Id);
            db.CatalogPages.Single(x => x.Id == HIDDEN_PAGE).ParentEntityId.Should().Be(shop.Id);
            db.CatalogOffers.Where(x => x.CatalogPageEntityId == seats.Id)
                .OrderBy(x => x.SortOrder)
                .Select(x => x.CostCredits)
                .Should()
                .Equal(1, 2, 5);
        }

        _service.History.Undo.Should().ContainSingle().Which.Label.Should().Be("made a shop");

        (await _service.UndoAsync(Editor, Ct)).Error.Should().BeNull();

        Snapshot().Should().Be(before);
    }

    [Fact]
    public async Task ATreeWithAnythingWrong_SavesNothing()
    {
        var before = Snapshot();
        var draft = new CatalogTreeDraft(
            [
                new CatalogNewPage(
                    CatalogPageRef.Saved(ROOT),
                    new CatalogPageDraft(
                        "Shop",
                        null,
                        2,
                        "default_3x3",
                        [],
                        [],
                        CatalogPageDisplay.Regular
                    )
                ),
            ],
            [
                new CatalogNewOffer(
                    CatalogPageRef.Made(0),
                    ChairOffer(0) with
                    {
                        CostCurrency = 5,
                        CurrencyTypeId = CREDITS_ROW,
                    }
                ),
            ],
            [],
            []
        );

        var result = await _service.BuildTreeAsync(Editor, "made a shop", draft, Ct);

        result.Saved.Should().BeFalse();
        result.Error.Should().Contain("currency");
        Snapshot().Should().Be(before);
        _service.History.Undo.Should().BeEmpty();
    }

    [Fact]
    public async Task ATreePage_PlacedFirst_PutsThePagesThereAfterIt()
    {
        var result = await _service.BuildTreeAsync(
            Editor,
            "added the front page",
            new CatalogTreeDraft(
                [
                    new CatalogNewPage(
                        CatalogPageRef.Saved(ROOT),
                        new CatalogPageDraft(
                            "Front Page",
                            null,
                            64,
                            "frontpage4",
                            [],
                            [],
                            CatalogPageDisplay.Regular
                        ),
                        Index: 0
                    ),
                ],
                [],
                [],
                []
            ),
            Ct
        );

        using var db = _catalog.Db.CreateDbContext();

        db.CatalogPages.Where(x => x.ParentEntityId == ROOT)
            .OrderBy(x => x.SortOrder)
            .Select(x => x.Id)
            .Should()
            .Equal(result.PageIds[0], FURNITURE, HIDDEN_PAGE);
    }
}
