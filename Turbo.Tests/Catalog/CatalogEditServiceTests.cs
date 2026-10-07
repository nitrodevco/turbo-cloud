using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Catalog.Editing;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// Editing the catalog from the admin panel: each edit is saved as made and checked first, so a
/// page stays in its tree and an offer only sells what the hotel can hand out; nothing that other
/// rows lean on is deleted; and players see the edits once they are published, when every client
/// online is told to refresh.
/// </summary>
public sealed class CatalogEditServiceTests : IDisposable
{
    private static readonly PlayerId Editor = 1;

    private readonly CatalogFixture _catalog = new();
    private readonly CapturingLogger<ICatalogEditService> _log = new();
    private readonly CatalogEditService _service;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public CatalogEditServiceTests()
    {
        _catalog.Fakes.Handlers["GetOnlinePlayerIds"] = _ => (IReadOnlyCollection<PlayerId>)[7, 8];
        _service = new CatalogEditService(
            _catalog.Db,
            _catalog.Definitions,
            _catalog.NormalProvider(),
            _catalog.BuildersClubProvider(),
            _catalog.Fakes.Create<ISessionGateway>(),
            _catalog.Fakes.Create<IGrainFactory>(),
            _log
        );
    }

    public void Dispose() => _catalog.Dispose();

    private static CatalogPageDraft Page(
        string title = "Lamps",
        string layout = "default_3x3",
        CatalogPageDisplay display = CatalogPageDisplay.Regular
    ) => new(title, null, 3, layout, ["header"], ["Bright", "lamps"], display);

    private static CatalogOfferDraft Offer(
        int pageId = FURNITURE,
        CatalogProductDraft? product = null,
        int credits = 10,
        int currency = 0,
        int? currencyRow = null
    ) =>
        new(
            pageId,
            string.Empty,
            credits,
            currency,
            currencyRow,
            true,
            true,
            0,
            true,
            product ?? new CatalogProductDraft(ProductType.Floor, CHAIR, null, 1)
        );

    private async Task<T> ReadAsync<T>(Func<Turbo.Database.Context.TurboDbContext, Task<T>> read)
    {
        await using var db = await _catalog.Db.CreateDbContextAsync(Ct);

        return await read(db);
    }

    [Fact]
    public async Task ANewPage_GoesLastUnderItsParent_InItsParentsCatalog()
    {
        var result = await _service.CreatePageAsync(Editor, ROOT, Page(), Ct);

        result.Saved.Should().BeTrue();

        var page = await ReadAsync(db => db.CatalogPages.SingleAsync(x => x.Id == result.Id, Ct));

        page.ParentEntityId.Should().Be(ROOT);
        page.SortOrder.Should().Be(2, "after Furniture (0) and Secret (1)");
        page.Localization.Should().Be("Lamps");
        page.TextData.Should().Equal("Bright", "lamps");
        _service.UnpublishedChanges.Should().Be(1);
    }

    [Theory]
    [InlineData("", "default_3x3")]
    [InlineData("Lamps", "")]
    [InlineData("Lamps", "two words")]
    public async Task APageWithoutATitleOrALayout_IsRefused(string title, string layout)
    {
        var result = await _service.CreatePageAsync(Editor, ROOT, Page(title, layout), Ct);

        result.Saved.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
        _service.UnpublishedChanges.Should().Be(0);
    }

    [Fact]
    public async Task MovingAPage_PutsItWhereAsked_AndNumbersItsNewSiblingsInOrder()
    {
        (await _service.MovePageAsync(Editor, HIDDEN_PAGE, ROOT, 0, Ct)).Saved.Should().BeTrue();

        (
            await ReadAsync(db =>
                db.CatalogPages.Where(x => x.ParentEntityId == ROOT)
                    .OrderBy(x => x.SortOrder)
                    .Select(x => x.Id)
                    .ToListAsync(Ct)
            )
        )
            .Should()
            .Equal(HIDDEN_PAGE, FURNITURE);
    }

    [Theory]
    [InlineData(FURNITURE, CHILD, "under itself")]
    [InlineData(FURNITURE, FURNITURE, "under itself")]
    [InlineData(ROOT, FURNITURE, "root")]
    [InlineData(BUILDERS_PAGE, ROOT, "Tabs aren't shown")]
    public async Task AMoveThatWouldBreakTheTree_IsRefused(int pageId, int parentId, string why)
    {
        var result = await _service.MovePageAsync(Editor, pageId, parentId, 0, Ct);

        result.Saved.Should().BeFalse();
        result.Error.Should().Contain(why);
    }

    [Fact]
    public async Task OnlyAnEmptyPage_IsDeleted()
    {
        (await _service.DeletePageAsync(Editor, FURNITURE, Ct))
            .Error.Should()
            .Contain("pages under it");
        (await _service.DeletePageAsync(Editor, HIDDEN_PAGE, Ct)).Error.Should().Contain("offers");
        (await _service.DeletePageAsync(Editor, ROOT, Ct)).Error.Should().Contain("root");

        (await _service.DeletePageAsync(Editor, CHILD, Ct)).Saved.Should().BeTrue();
        (await ReadAsync(db => db.CatalogPages.AnyAsync(x => x.Id == CHILD, Ct)))
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task ANewOffer_IsNamedByItsItem_AndGivesIt()
    {
        var result = await _service.CreateOfferAsync(Editor, Offer(), Ct);

        result.Saved.Should().BeTrue();

        var offer = await ReadAsync(db =>
            db.CatalogOffers.Include(x => x.Products).SingleAsync(x => x.Id == result.Id, Ct)
        );

        offer.LocalizationId.Should().Be("chair");
        offer.CostCredits.Should().Be(10);
        offer
            .Products.Should()
            .ContainSingle()
            .Which.FurnitureDefinitionEntityId.Should()
            .Be(CHAIR);
    }

    [Fact]
    public async Task AnOfferMustGiveSomethingTheHotelCanHandOut()
    {
        (
            await _service.CreateOfferAsync(
                Editor,
                Offer(product: new(ProductType.Floor, null, null, 1)),
                Ct
            )
        )
            .Error.Should()
            .Contain("Choose the item");
        (
            await _service.CreateOfferAsync(
                Editor,
                Offer(product: new(ProductType.Floor, POSTER, null, 1)),
                Ct
            )
        )
            .Error.Should()
            .Be("poster is a wall item.");
        (
            await _service.CreateOfferAsync(
                Editor,
                Offer(product: new(ProductType.Badge, null, " ", 1)),
                Ct
            )
        )
            .Error.Should()
            .Contain("badge code");
        (
            await _service.CreateOfferAsync(
                Editor,
                Offer(product: new(ProductType.Pet, null, "0", 1)),
                Ct
            )
        )
            .Saved.Should()
            .BeFalse();
        (
            await _service.CreateOfferAsync(
                Editor,
                Offer(product: new(ProductType.Floor, CHAIR, null, 0)),
                Ct
            )
        )
            .Error.Should()
            .Contain("1 to");
    }

    [Fact]
    public async Task ASecondPrice_IsInAnActivityPointCurrency()
    {
        (await _service.CreateOfferAsync(Editor, Offer(currency: 5), Ct))
            .Error.Should()
            .Contain("which currency");
        (await _service.CreateOfferAsync(Editor, Offer(currency: 5, currencyRow: CREDITS_ROW), Ct))
            .Error.Should()
            .Contain("not an activity-point currency");

        var ok = await _service.CreateOfferAsync(
            Editor,
            Offer(currency: 5, currencyRow: DUCKETS_ROW),
            Ct
        );

        ok.Saved.Should().BeTrue();
        (await ReadAsync(db => db.CatalogOffers.SingleAsync(x => x.Id == ok.Id, Ct)))
            .CurrencyTypeId.Should()
            .Be(DUCKETS_ROW);
    }

    [Fact]
    public async Task AnOffer_MovesToAPageOfTheOtherCatalog()
    {
        var result = await _service.UpdateOfferAsync(
            Editor,
            SOLD,
            Offer(pageId: BUILDERS_PAGE),
            Ct
        );

        result.Saved.Should().BeTrue();
        (await ReadAsync(db => db.CatalogOffers.SingleAsync(x => x.Id == SOLD, Ct)))
            .CatalogPageEntityId.Should()
            .Be(BUILDERS_PAGE);
    }

    [Fact]
    public async Task ATab_IsNotShownInTheBuildersClubCatalog()
    {
        var created = await _service.CreatePageAsync(
            Editor,
            ROOT,
            Page(display: CatalogPageDisplay.Both),
            Ct
        );
        var saved = await _service.UpdatePageAsync(
            Editor,
            FURNITURE,
            Page("Furniture", display: CatalogPageDisplay.BuildersClubOnly),
            Ct
        );

        created.Error.Should().Contain("Tabs aren't shown");
        saved.Error.Should().Contain("Tabs aren't shown");
        (
            await _service.CreatePageAsync(
                Editor,
                FURNITURE,
                Page(display: CatalogPageDisplay.Both),
                Ct
            )
        )
            .Saved.Should()
            .BeTrue("a page under a tab is shown there");
    }

    [Fact]
    public async Task APageSellingAMembership_StaysInTheNormalCatalog()
    {
        (await _service.CreateOfferAsync(Editor, Offer(pageId: CHILD, product: Membership(31)), Ct))
            .Saved.Should()
            .BeTrue();

        (
            await _service.UpdatePageAsync(
                Editor,
                CHILD,
                Page("Chairs", display: CatalogPageDisplay.BuildersClubOnly),
                Ct
            )
        )
            .Error.Should()
            .Contain("only the normal catalog sells");
        (
            await _service.UpdatePageAsync(
                Editor,
                CHILD,
                Page("Chairs", display: CatalogPageDisplay.Both),
                Ct
            )
        )
            .Saved.Should()
            .BeTrue("the normal catalog still sells it");
    }

    [Fact]
    public async Task ALimitedSeries_KeepsItsItem_AndItsOfferIsNotDeleted()
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

        (
            await _service.UpdateOfferAsync(
                Editor,
                SOLD,
                Offer(product: new(ProductType.Wall, POSTER, null, 1)),
                Ct
            )
        )
            .Error.Should()
            .Contain("limited series");
        (await _service.DeleteOfferAsync(Editor, SOLD, Ct))
            .Error.Should()
            .Contain("limited series");

        // Its price can still change, and what is left of the series is not touched.
        (await _service.UpdateOfferAsync(Editor, SOLD, Offer(credits: 99), Ct))
            .Saved.Should()
            .BeTrue();
        (await ReadAsync(db => db.LtdSeries.SingleAsync(Ct))).RemainingQuantity.Should().Be(40);
    }

    [Fact]
    public async Task AnOfferBuildersClubFurniWasPlacedFrom_IsNotDeleted()
    {
        // The furni sits in a room someone placed it in.
        _catalog.Db.Insert(
            new PlayerEntity
            {
                Id = 1,
                Name = "builder",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        _catalog.Db.Insert(
            new RoomModelEntity
            {
                Id = 1,
                Name = "model_a",
                Model = "00\r00",
                DoorX = 0,
                DoorY = 0,
                DoorRotation = Rotation.North,
                Enabled = true,
                Custom = false,
            }
        );
        _catalog.Db.Insert(
            new RoomEntity
            {
                Id = 1,
                Name = "Studio",
                PlayerEntityId = 1,
                RoomModelEntityId = 1,
                DoorMode = RoomDoorModeType.Open,
                UsersNow = 0,
                PlayersMax = 25,
                WallHeight = -1,
                HideWalls = false,
                ThicknessWall = RoomThicknessType.Normal,
                ThicknessFloor = RoomThicknessType.Normal,
                AllowBlocking = true,
                AllowPets = true,
                AllowPetsEat = true,
                TradeType = RoomTradeModeType.Disabled,
                MuteType = ModSettingType.Owner,
                KickType = ModSettingType.Owner,
                BanType = ModSettingType.Owner,
                ChatFloodType = ChatFloodSensitivityType.Minimal,
                PlayerEntity = null!,
                RoomModelEntity = null!,
            }
        );
        _catalog.Db.Insert(
            new BuildersClubFurnitureEntity
            {
                RoomEntityId = 1,
                RoomObjectId = 1,
                FurnitureDefinitionEntityId = CHAIR,
                PlacedByPlayerEntityId = 1,
                CatalogOfferEntityId = SOLD,
            }
        );

        (await _service.DeleteOfferAsync(Editor, SOLD, Ct)).Error.Should().Contain("Builders Club");
        (await _service.DeleteOfferAsync(Editor, IN_DIAMONDS, Ct)).Saved.Should().BeTrue();
    }

    private static CatalogProductDraft Membership(
        int days,
        SubscriptionType? type = SubscriptionType.HabboClub,
        int quantity = 1
    ) => new(ProductType.HabboClub, null, null, quantity, type, days);

    [Fact]
    public async Task AMembership_IsDaysOfClub_NamedByItsLength_ForAnyone_AndNotGifted()
    {
        var result = await _service.CreateOfferAsync(
            Editor,
            Offer(pageId: HIDDEN_PAGE, product: Membership(93), credits: 60),
            Ct
        );

        result.Saved.Should().BeTrue(result.Error);

        var offer = await ReadAsync(db =>
            db.CatalogOffers.Include(x => x.Products).SingleAsync(x => x.Id == result.Id, Ct)
        );
        var product = offer.Products.Should().ContainSingle().Subject;

        offer.LocalizationId.Should().Be("habbo_club_3_months");
        offer.CanGift.Should().BeFalse("a membership has no gift path");
        offer.CanBundle.Should().BeFalse();
        product.ProductType.Should().Be(ProductType.HabboClub);
        product.SubscriptionType.Should().Be(SubscriptionType.HabboClub);
        product.SubscriptionDays.Should().Be(93);
        product.FurnitureDefinitionEntityId.Should().BeNull();
    }

    [Fact]
    public async Task AMembership_IsOnlyWhatTheClubWindowCanSell()
    {
        (
            await _service.CreateOfferAsync(
                Editor,
                Offer(pageId: BUILDERS_PAGE, product: Membership(31)),
                Ct
            )
        )
            .Error.Should()
            .Contain("normal catalog");
        (
            await _service.CreateOfferAsync(
                Editor,
                Offer(product: Membership(31)) with
                {
                    ClubLevel = 1,
                },
                Ct
            )
        )
            .Error.Should()
            .Contain("for anyone");
        (await _service.CreateOfferAsync(Editor, Offer(product: Membership(31, type: null)), Ct))
            .Error.Should()
            .Contain("which membership");
        (await _service.CreateOfferAsync(Editor, Offer(product: Membership(0)), Ct))
            .Error.Should()
            .Contain("days");
        (await _service.CreateOfferAsync(Editor, Offer(product: Membership(31, quantity: 2)), Ct))
            .Error.Should()
            .Contain("one at a time");
        (
            await _service.CreateOfferAsync(
                Editor,
                Offer(product: Membership(45, SubscriptionType.BuildersClub)),
                Ct
            )
        )
            .Saved.Should()
            .BeTrue();
    }

    [Fact]
    public async Task AClubGift_IsFurni_ClaimedByAKeyNoOtherGiftHas()
    {
        var gift = await _service.CreateOfferAsync(
            Editor,
            Offer() with
            {
                LocalizationId = "gift_chair",
                ClubGiftDaysRequired = 31,
            },
            Ct
        );

        gift.Saved.Should().BeTrue(gift.Error);
        (await ReadAsync(db => db.CatalogOffers.SingleAsync(x => x.Id == gift.Id, Ct)))
            .ClubGiftDaysRequired.Should()
            .Be(31);

        (
            await _service.CreateOfferAsync(
                Editor,
                Offer() with
                {
                    LocalizationId = "gift_chair",
                    ClubGiftDaysRequired = 62,
                },
                Ct
            )
        )
            .Error.Should()
            .Contain("Another club gift");
        (
            await _service.CreateOfferAsync(
                Editor,
                Offer(product: new(ProductType.Badge, null, "ADM", 1)) with
                {
                    ClubGiftDaysRequired = 0,
                },
                Ct
            )
        )
            .Error.Should()
            .Contain("gives furni");
        (
            await _service.CreateOfferAsync(
                Editor,
                Offer(pageId: BUILDERS_PAGE) with
                {
                    ClubGiftDaysRequired = 0,
                },
                Ct
            )
        )
            .Error.Should()
            .Contain("normal catalog");

        // The same gift saved again keeps its own key.
        (
            await _service.UpdateOfferAsync(
                Editor,
                gift.Id,
                Offer() with
                {
                    LocalizationId = "gift_chair",
                    ClubGiftDaysRequired = 93,
                },
                Ct
            )
        )
            .Saved.Should()
            .BeTrue();
    }

    private static CatalogLimitedDraft Limited(int total, int window = 30) =>
        new(total, window, null, null, true);

    [Fact]
    public async Task ALimitedSeries_StartsWithAllOfItLeft()
    {
        (await _service.SaveLimitedAsync(Editor, SOLD, Limited(50), Ct)).Saved.Should().BeTrue();

        var series = await ReadAsync(db => db.LtdSeries.SingleAsync(Ct));

        series.CatalogProductEntityId.Should().Be(SOLD);
        series.TotalQuantity.Should().Be(50);
        series.RemainingQuantity.Should().Be(50);
        series.RaffleWindowSeconds.Should().Be(30);
    }

    [Fact]
    public async Task ANewTotal_MovesWhatIsLeft_AndIsNeverBelowWhatIsSold()
    {
        await _service.SaveLimitedAsync(Editor, SOLD, Limited(50), Ct);
        await ReadAsync(db =>
            db.LtdSeries.ExecuteUpdateAsync(s => s.SetProperty(x => x.RemainingQuantity, 45), Ct)
        );

        (await _service.SaveLimitedAsync(Editor, SOLD, Limited(60), Ct)).Saved.Should().BeTrue();

        var series = await ReadAsync(db => db.LtdSeries.SingleAsync(Ct));

        series.TotalQuantity.Should().Be(60);
        series.RemainingQuantity.Should().Be(55, "5 are sold, so 55 of 60 are left");
        (await _service.SaveLimitedAsync(Editor, SOLD, Limited(4), Ct))
            .Error.Should()
            .Contain("5 of it are sold");
        (await _service.SaveLimitedAsync(Editor, SOLD, Limited(5), Ct))
            .Saved.Should()
            .BeTrue("exactly what is sold");

        // An open raffle reads the series again.
        _catalog.Fakes.Log.Of("ReloadSeriesAsync").Should().NotBeEmpty();
    }

    [Fact]
    public async Task ALimitedSeries_IsOneFurniItem_OnASoldOffer()
    {
        var gift = await _service.CreateOfferAsync(
            Editor,
            Offer() with
            {
                LocalizationId = "gift",
                ClubGiftDaysRequired = 0,
            },
            Ct
        );
        var membership = await _service.CreateOfferAsync(
            Editor,
            Offer(product: Membership(31)),
            Ct
        );

        (await _service.SaveLimitedAsync(Editor, gift.Id, Limited(10), Ct))
            .Error.Should()
            .Contain("club gift");
        (await _service.SaveLimitedAsync(Editor, membership.Id, Limited(10), Ct))
            .Error.Should()
            .Contain("floor or wall");
        (await _service.SaveLimitedAsync(Editor, SOLD, Limited(0), Ct)).Saved.Should().BeFalse();
        (await _service.SaveLimitedAsync(Editor, SOLD, Limited(10, window: -1), Ct))
            .Saved.Should()
            .BeFalse();
        (
            await _service.SaveLimitedAsync(
                Editor,
                SOLD,
                new CatalogLimitedDraft(10, 0, DateTime.UtcNow, DateTime.UtcNow.AddHours(-1), true),
                Ct
            )
        )
            .Error.Should()
            .Contain("ends after it starts");
    }

    [Fact]
    public async Task ASeries_ComesOffOnlyWhileNoneOfItIsSold()
    {
        await _service.SaveLimitedAsync(Editor, SOLD, Limited(50), Ct);
        await _service.SaveLimitedAsync(Editor, IN_DIAMONDS, Limited(50), Ct);
        await ReadAsync(db =>
            db.LtdSeries.Where(x => x.CatalogProductEntityId == SOLD)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RemainingQuantity, 49), Ct)
        );

        (await _service.RemoveLimitedAsync(Editor, SOLD, Ct)).Error.Should().Contain("sold");
        (await _service.RemoveLimitedAsync(Editor, IN_DIAMONDS, Ct)).Saved.Should().BeTrue();
        (await ReadAsync(db => db.LtdSeries.CountAsync(Ct))).Should().Be(1);
        (await _service.RemoveLimitedAsync(Editor, IN_DIAMONDS, Ct))
            .Error.Should()
            .Contain("not a limited");
    }

    [Fact]
    public async Task Publishing_PutsTheEditsLive_AndTellsEveryoneOnline()
    {
        var provider = _catalog.NormalProvider();
        var service = new CatalogEditService(
            _catalog.Db,
            _catalog.Definitions,
            provider,
            _catalog.BuildersClubProvider(),
            _catalog.Fakes.Create<ISessionGateway>(),
            _catalog.Fakes.Create<IGrainFactory>(),
            _log
        );

        await provider.ReloadAsync(Ct);
        var created = await service.CreatePageAsync(Editor, ROOT, Page(), Ct);

        provider.Current.PagesById.Should().NotContainKey(created.Id, "not live until published");

        var published = await service.PublishAsync(Editor, Ct);

        provider.Current.PagesById.Should().ContainKey(created.Id);
        published.PlayersTold.Should().Be(2);
        service.UnpublishedChanges.Should().Be(0);
        _catalog
            .Fakes.Log.Calls.Where(x =>
                x.Method == "SendComposerAsync" && x.Args[0] is CatalogPublishedMessageComposer
            )
            .Select(x => Convert.ToInt32(x.Key))
            .Should()
            .BeEquivalentTo([7, 8]);
    }
}
