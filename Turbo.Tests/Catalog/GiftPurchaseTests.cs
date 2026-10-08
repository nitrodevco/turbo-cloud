using System.Reflection;
using FluentAssertions;
using Orleans;
using Orleans.Runtime;
using Turbo.Catalog;
using Turbo.Catalog.Exceptions;
using Turbo.Database.Entities.Catalog;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// Buying an offer as a gift from the catalog's gift dialog: the receiver's inventory is asked
/// to wrap it as the buyer chose, a paid wrapping costs its price on top, and everything that
/// cannot be given is refused before the buyer is charged.
/// </summary>
public sealed class GiftPurchaseTests : IDisposable
{
    private const int BUYER = 7;
    private const int RECEIVER = 8;

    private const int WRAP = 30;
    private const int FREE_BOX = 31;
    private const int TELEPORT = 32;
    private const int TELEPORT_OFFER = 300;
    private const int UNGIFTABLE_OFFER = 301;

    private const int WRAPPING_PRICE = 2;

    private readonly CatalogFixture _catalog = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public GiftPurchaseTests()
    {
        _catalog.AddDefinition(WRAP, "present_wrap*1", set: x => x.Logic = PresentData.LOGIC_NAME);
        _catalog.AddDefinition(FREE_BOX, "present_gen", set: x => x.Logic = PresentData.LOGIC_NAME);
        _catalog.AddDefinition(
            TELEPORT,
            "teleport",
            set: x => x.Logic = TeleportFurniture.LOGIC_NAME
        );
        AddOffer(TELEPORT_OFFER, TELEPORT, canGift: true);
        AddOffer(UNGIFTABLE_OFFER, CHAIR, canGift: false);

        var fakes = _catalog.Fakes;

        fakes.Handlers["TryGetDefinitionBySprite"] = call =>
            _catalog.Definitions.TryGetDefinition((int)call.Args[1]!);
        fakes.Handlers["GetWrapping"] = _ => new GiftWrappingSnapshot
        {
            Enabled = true,
            Price = WRAPPING_PRICE,
            StuffTypes = [WRAP],
            BoxTypes = [0, 3],
            RibbonTypes = [0, 5],
            DefaultStuffTypes = [FREE_BOX],
        };
        fakes.Handlers["GetPlayerIdAsync"] = call =>
            Task.FromResult<PlayerId?>(
                (string)call.Args[0]! == "friend" ? (PlayerId)RECEIVER : null
            );
        fakes.Handlers["GetSummaryAsync"] = call => Task.FromResult(Summary((long)call.Key!));
        fakes.Handlers["TryDebitAsync"] = _ => Task.FromResult(WalletDebitResult.Success());
    }

    public void Dispose() => _catalog.Dispose();

    [Fact]
    public async Task AGift_IsWrappedInTheReceiversInventory_AsTheBuyerChose()
    {
        await GiveAsync(Request(sprite: WRAP, box: 3, ribbon: 5, message: " Happy birthday "));

        var call = Grants().Should().ContainSingle().Subject;
        var grant = (PresentGrantRequest)call.Args[0]!;

        call.Key.Should().Be((long)RECEIVER);
        grant.Product.FurniDefinitionId.Should().Be(CHAIR);
        grant.PresentDefinitionId.Should().Be(WRAP);
        grant.BoxType.Should().Be(3);
        grant.RibbonType.Should().Be(5);
        grant.Message.Should().Be("Happy birthday");
        grant.PurchaserName.Should().Be("buyer");
        grant.PurchaserFigure.Should().Be("hd-180-1");
    }

    [Fact]
    public async Task APaidWrapping_CostsItsPriceInCreditsOnTop()
    {
        await GiveAsync(Request(sprite: WRAP, box: 3, ribbon: 5));

        Debited().Should().ContainSingle().Which.Amount.Should().Be(5 + WRAPPING_PRICE);
    }

    [Fact]
    public async Task TheFreeBox_CostsOnlyTheOffer_AndCarriesNoStyle()
    {
        await GiveAsync(Request(sprite: FREE_BOX, box: 0, ribbon: 0));

        Debited().Should().ContainSingle().Which.Amount.Should().Be(5);
        var grant = (PresentGrantRequest)Grants().Single().Args[0]!;
        grant.PresentDefinitionId.Should().Be(FREE_BOX);
        grant.BoxType.Should().Be(0);
        grant.RibbonType.Should().Be(0);
    }

    [Fact]
    public async Task AnAnonymousGift_HasABlankTag_ButStillNamesTheBuyerForEngraving()
    {
        await GiveAsync(Request(sprite: FREE_BOX, showName: false));

        var grant = (PresentGrantRequest)Grants().Single().Args[0]!;
        grant.PurchaserName.Should().BeNull();
        grant.PurchaserFigure.Should().BeNull();
        grant.BuyerName.Should().Be("buyer");
    }

    [Theory]
    [InlineData("nobody")]
    [InlineData("")]
    public async Task AReceiverNobodyIs_IsNotFound(string receiver) =>
        await RefusedAsync(
            Request(sprite: FREE_BOX, receiver: receiver),
            CatalogPurchaseErrorType.ReceiverNotFound
        );

    [Fact]
    public async Task AReceiverWhoBlockedTheBuyer_RefusesIt()
    {
        _catalog.Fakes.Handlers["IsBlockingAsync"] = call =>
            Task.FromResult((long)call.Key! == RECEIVER && (PlayerId)call.Args[0]! == BUYER);

        await RefusedAsync(Request(sprite: FREE_BOX), CatalogPurchaseErrorType.BlockedByReceiver);
    }

    [Fact]
    public async Task ANoteTooLongForTheTag_IsRefused() =>
        await RefusedAsync(
            Request(sprite: FREE_BOX, message: new string('x', 141)),
            CatalogPurchaseErrorType.InvalidGiftMessage
        );

    [Theory]
    [InlineData(WRAP, 7, 5)] // a box style not offered
    [InlineData(WRAP, 3, 9)] // a ribbon not offered
    [InlineData(FREE_BOX, 3, 5)] // the free box with a paid style
    [InlineData(CHAIR, 0, 0)] // not a present at all
    public async Task AWrappingTheDialogNeverOffered_IsRefused(int sprite, int box, int ribbon) =>
        await RefusedAsync(
            Request(sprite: sprite, box: box, ribbon: ribbon),
            CatalogPurchaseErrorType.PurchaseFailed
        );

    [Fact]
    public async Task AnOfferThatCannotBeGifted_IsRefused() =>
        await RefusedAsync(
            Request(sprite: FREE_BOX, offerId: UNGIFTABLE_OFFER),
            CatalogPurchaseErrorType.PurchaseFailed
        );

    [Fact]
    public async Task ATeleporter_WhichIsAPair_IsRefused() =>
        await RefusedAsync(
            Request(sprite: FREE_BOX, offerId: TELEPORT_OFFER),
            CatalogPurchaseErrorType.PurchaseFailed
        );

    [Fact]
    public async Task AFailedWrapping_RefundsTheBuyer()
    {
        _catalog.Fakes.Handlers["ReceivePresentAsync"] = _ =>
            Task.FromException(new InvalidOperationException("inventory down"));

        var give = () => GiveAsync(Request(sprite: WRAP, box: 3, ribbon: 5));

        await give.Should().ThrowAsync<InvalidOperationException>();
        _catalog
            .Fakes.Log.Of("CreditAsync")
            .Should()
            .ContainSingle()
            .Which.Args.Should()
            .Contain(5 + WRAPPING_PRICE);
    }

    private async Task RefusedAsync(CatalogGiftRequest request, CatalogPurchaseErrorType error)
    {
        var give = () => GiveAsync(request);

        (await give.Should().ThrowAsync<CatalogPurchaseException>())
            .Which.ErrorType.Should()
            .Be(error);
        _catalog.Fakes.Log.Of("TryDebitAsync").Should().BeEmpty("nothing is charged");
        Grants().Should().BeEmpty();
    }

    private void AddOffer(int id, int definitionId, bool canGift)
    {
        _catalog.Db.Insert(
            new CatalogOfferEntity
            {
                Id = id,
                CatalogPageEntityId = FURNITURE,
                LocalizationId = "offer",
                CostCredits = 5,
                CostCurrency = 0,
                CanGift = canGift,
                CanBundle = true,
                ClubLevel = 0,
                Visible = true,
                Page = null!,
            }
        );
        _catalog.Db.Insert(
            new CatalogProductEntity
            {
                Id = id,
                CatalogOfferEntityId = id,
                ProductType = ProductType.Floor,
                FurnitureDefinitionEntityId = definitionId,
                Quantity = 1,
                Offer = null!,
            }
        );
    }

    private IEnumerable<FakeCall> Grants() => _catalog.Fakes.Log.Of("ReceivePresentAsync");

    private IEnumerable<WalletDebitRequest> Debited() =>
        _catalog
            .Fakes.Log.Of("TryDebitAsync")
            .SelectMany(x => (IEnumerable<WalletDebitRequest>)x.Args[0]!);

    private static CatalogGiftRequest Request(
        int sprite,
        int box = 0,
        int ribbon = 0,
        string receiver = "friend",
        string message = "",
        bool showName = true,
        int offerId = SOLD
    ) =>
        new()
        {
            OfferId = offerId,
            ExtraParam = string.Empty,
            ReceiverName = receiver,
            Message = message,
            SpriteId = sprite,
            BoxType = box,
            RibbonType = ribbon,
            ShowPurchaserName = showName,
        };

    private static PlayerSummarySnapshot Summary(long playerId) =>
        new()
        {
            PlayerId = (int)playerId,
            Name = "buyer",
            Motto = string.Empty,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            AchievementScore = 0,
            BadgesRank = 0,
            IsOnline = true,
            CreatedAt = DateTime.UnixEpoch,
            LastUpdated = DateTime.UnixEpoch,
            RespectPoints = 0,
            RespectsLeft = 0,
            PetRespectsLeft = 0,
            RespectReplenishesLeft = 0,
        };

    /// <summary>The buyer's purchase grain giving an offer from the catalog as published.</summary>
    private async Task GiveAsync(CatalogGiftRequest request)
    {
        var provider = _catalog.NormalProvider();

        await provider.ReloadAsync(Ct);

        var fakes = _catalog.Fakes;

        fakes.Handlers["GetCatalogSnapshot"] = _ => provider.Current;

        var grain = GrainHarness.Create(
            typeof(CatalogModule).Assembly,
            "Turbo.Catalog.Grains.CatalogPurchaseGrain",
            fakes,
            _catalog.Db,
            playerId: BUYER
        );

        Set(grain, "_catalogService", fakes.Create<ICatalogService>());
        Set(grain, "_definitionProvider", _catalog.Definitions);
        Set(
            grain,
            "_giftWrappingProvider",
            fakes.Create<Turbo.Primitives.Catalog.Providers.IGiftWrappingProvider>()
        );
        typeof(Grain)
            .GetProperty(
                "GrainContext",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
            )!
            .GetSetMethod(true)!
            .Invoke(
                grain,
                [
                    GrainContextStub.Create(
                        GrainId.Create(
                            GrainType.Create("catalogpurchase"),
                            GrainIdKeyExtensions.CreateIntegerKey(BUYER)
                        )
                    ),
                ]
            );

        await ((ICatalogPurchaseGrain)grain).PurchaseOfferAsGiftAsync(request, Ct);
    }

    private static void Set(object grain, string field, object value) =>
        grain
            .GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(grain, value);
}
