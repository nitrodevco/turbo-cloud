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
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Players.Wallet;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// Buying furni that is what a parameter names: a badge display shows a badge the buyer picked,
/// which they have to own, and a paper, poster or song disc is what its product names. Each is
/// refused before the buyer is charged when it cannot be handed out.
/// </summary>
public sealed class ProductParamPurchaseTests : IDisposable
{
    private const int BADGE_DISPLAY = 20;
    private const int WALLPAPER = 21;
    private const int SONG_DISK = 22;

    private const int DISPLAY_OFFER = 300;
    private const int PAPER_OFFER = 301;
    private const int BLANK_PAPER_OFFER = 302;
    private const int SONG_OFFER = 303;
    private const int NO_SONG_OFFER = 304;

    private readonly CatalogFixture _catalog = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ProductParamPurchaseTests()
    {
        _catalog.AddDefinition(
            BADGE_DISPLAY,
            "badge_display",
            set: x => x.Logic = BadgeDisplayData.LOGIC_NAME
        );
        _catalog.AddDefinition(
            WALLPAPER,
            "wallpaper",
            ProductType.Wall,
            x =>
            {
                x.Logic = "default_wall";
                x.FurniCategory = FurnitureCategory.WallPaper;
            }
        );
        _catalog.AddDefinition(
            SONG_DISK,
            "song_disk",
            set: x => x.FurniCategory = FurnitureCategory.TraxSong
        );

        AddOffer(DISPLAY_OFFER, BADGE_DISPLAY, ProductType.Floor, null);
        AddOffer(PAPER_OFFER, WALLPAPER, ProductType.Wall, "101");
        AddOffer(BLANK_PAPER_OFFER, WALLPAPER, ProductType.Wall, null);
        AddOffer(SONG_OFFER, SONG_DISK, ProductType.Floor, "5");
        AddOffer(NO_SONG_OFFER, SONG_DISK, ProductType.Floor, "my song");
    }

    public void Dispose() => _catalog.Dispose();

    [Fact]
    public async Task ABadgeDisplay_WithABadgeTheBuyerOwns_IsSoldWithThatBadge()
    {
        _catalog.Fakes.Handlers["HasBadgeAsync"] = call =>
            Task.FromResult((string)call.Args[0]! == "ADM");

        await BuyAsync(DISPLAY_OFFER, " ADM ");

        _catalog.Fakes.Log.Of("TryDebitAsync").Should().ContainSingle();
        _catalog
            .Fakes.Log.Of("GrantCatalogOfferAsync")
            .Should()
            .ContainSingle()
            .Which.Args[1]
            .Should()
            .Be(" ADM ", "the inventory engraves the trimmed code");
    }

    [Theory]
    [InlineData("XYZ")]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ABadgeDisplay_WithABadgeTheBuyerDoesNotOwn_IsRefusedBeforeCharging(
        string badgeCode
    )
    {
        _catalog.Fakes.Handlers["HasBadgeAsync"] = call =>
            Task.FromResult((string)call.Args[0]! == "ADM");

        await RefusedBeforeChargingAsync(DISPLAY_OFFER, badgeCode);
    }

    [Fact]
    public async Task APaperOrSongDisc_ThatNamesSomething_IsSold()
    {
        await BuyAsync(PAPER_OFFER, string.Empty);
        await BuyAsync(SONG_OFFER, string.Empty);

        _catalog.Fakes.Log.Of("GrantCatalogOfferAsync").Should().HaveCount(2);
    }

    [Theory]
    [InlineData(BLANK_PAPER_OFFER)]
    [InlineData(NO_SONG_OFFER)]
    public async Task AProductThatNamesNothing_IsRefusedBeforeCharging(int offerId) =>
        await RefusedBeforeChargingAsync(offerId, "101");

    private async Task RefusedBeforeChargingAsync(int offerId, string extraParam)
    {
        var buy = () => BuyAsync(offerId, extraParam);

        (await buy.Should().ThrowAsync<CatalogPurchaseException>())
            .Which.ErrorType.Should()
            .Be(CatalogPurchaseErrorType.PurchaseFailed);
        _catalog.Fakes.Log.Of("TryDebitAsync").Should().BeEmpty("nothing is charged");
        _catalog.Fakes.Log.Of("GrantCatalogOfferAsync").Should().BeEmpty();
    }

    private void AddOffer(int id, int definitionId, ProductType type, string? extraParam)
    {
        _catalog.Db.Insert(
            new CatalogOfferEntity
            {
                Id = id,
                CatalogPageEntityId = FURNITURE,
                LocalizationId = "offer",
                CostCredits = 3,
                CostCurrency = 0,
                CanGift = true,
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
                ProductType = type,
                FurnitureDefinitionEntityId = definitionId,
                ExtraParam = extraParam,
                Quantity = 1,
                Offer = null!,
            }
        );
    }

    /// <summary>The buyer's purchase grain buying an offer from the catalog as published.</summary>
    private async Task BuyAsync(int offerId, string extraParam)
    {
        var provider = _catalog.NormalProvider();

        await provider.ReloadAsync(Ct);

        var fakes = _catalog.Fakes;

        fakes.Handlers["GetCatalogSnapshot"] = _ => provider.Current;
        fakes.Handlers["TryDebitAsync"] = _ => Task.FromResult(WalletDebitResult.Success());

        var grain = GrainHarness.Create(
            typeof(CatalogModule).Assembly,
            "Turbo.Catalog.Grains.CatalogPurchaseGrain",
            fakes,
            _catalog.Db,
            playerId: 7
        );

        Set(grain, "_catalogService", fakes.Create<ICatalogService>());
        Set(grain, "_definitionProvider", _catalog.Definitions);
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
                            GrainIdKeyExtensions.CreateIntegerKey(7)
                        )
                    ),
                ]
            );

        await ((ICatalogPurchaseGrain)grain).PurchaseOfferFromCatalogAsync(
            CatalogType.Normal,
            offerId,
            extraParam,
            1,
            Ct
        );
    }

    private static void Set(object grain, string field, object value) =>
        grain
            .GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(grain, value);
}
