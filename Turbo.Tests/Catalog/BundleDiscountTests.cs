using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Turbo.Catalog;
using Turbo.Catalog.Configuration;
using Turbo.Catalog.Exceptions;
using Turbo.Database.Entities.Catalog;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Catalog;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Wallet;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// Habbo answers GetBundleDiscountRuleset when the catalog opens (hhus, 2026-10-12, seven
/// samples: up to 100, no basic bundle, a free item at 5, 9, 9, 13, 13 ... 97, 97); Turbo sent
/// nothing, so the spinner had no maximum, and a purchase took any quantity of any offer.
/// </summary>
public sealed class BundleDiscountTests : IDisposable
{
    private const int UNBUNDLED = 200;

    private readonly CatalogFixture _catalog = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public BundleDiscountTests()
    {
        _catalog.Db.Insert(
            new CatalogOfferEntity
            {
                Id = UNBUNDLED,
                CatalogPageEntityId = FURNITURE,
                LocalizationId = "chair",
                CostCredits = 3,
                CostCurrency = 0,
                CanGift = true,
                CanBundle = false,
                ClubLevel = 0,
                Visible = true,
                Page = null!,
            }
        );
        _catalog.Db.Insert(
            new CatalogProductEntity
            {
                Id = UNBUNDLED,
                CatalogOfferEntityId = UNBUNDLED,
                ProductType = ProductType.Floor,
                FurnitureDefinitionEntityId = CHAIR,
                Quantity = 1,
                Offer = null!,
            }
        );
    }

    public void Dispose() => _catalog.Dispose();

    [Fact]
    public async Task the_ruleset_is_sent_as_habbo_sends_it()
    {
        var fakes = new Fakes();

        await new GetBundleDiscountRulesetMessageHandler(
            Options.Create(new CatalogConfig())
        ).HandleAsync(
            new GetBundleDiscountRulesetMessage(),
            new MessageContext(fakes.Create<ISessionContext>(), 1, -1),
            Ct
        );

        var composer = Assert.IsType<BundleDiscountRulesetMessageComposer>(
            fakes.Log.Of("SendComposerAsync").Single().Args[0]
        );
        var packet = PacketHarness.Encode(composer);

        // The official reply, byte for byte.
        Assert.Equal(
            Convert.FromHexString(
                "000000640000000000000000FFFFFFFF0000002F00000005000000090000000900"
                    + "00000D0000000D0000001100000011000000150000001500000019000000190000"
                    + "001D0000001D0000002100000021000000250000002500000029000000290000002D"
                    + "0000002D0000003100000031000000350000003500000039000000390000003D0000"
                    + "003D0000004100000041000000450000004500000049000000490000004D0000004D"
                    + "0000005100000051000000550000005500000059000000590000005D0000005D0000"
                    + "006100000061"
            ),
            packet.PopBytes(packet.Remaining)
        );
    }

    [Fact]
    public async Task the_most_the_spinner_allows_is_sold_with_its_free_items()
    {
        await BuyAsync(SOLD, 100);

        // 5, 9, 9, 13, 13 ... 97, 97: all 47 listed quantities are reached.
        Granted().Should().Be(147);
        Charged().Should().Be(500, "the free items cost nothing");
    }

    /// <summary>
    /// The spinner showed 10 with "3 free" (one at 5, two at 9) and the confirmation the price of
    /// 10; the purchase gave 10 and charged for 10, so the free items the client promised never came.
    /// </summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(4, 4)]
    [InlineData(5, 6)]
    [InlineData(10, 13)]
    public async Task the_free_items_the_client_shows_are_given_and_not_charged(
        int quantity,
        int granted
    )
    {
        await BuyAsync(SOLD, quantity);

        Granted().Should().Be(granted);
        Charged().Should().Be(quantity * 5);
    }

    [Fact]
    public async Task a_basic_bundle_gives_its_free_items_per_whole_bundle()
    {
        var config = new CatalogConfig
        {
            BundleDiscount = new BundleDiscountConfig
            {
                BundleSize = 6,
                BundleDiscountSize = 1,
                BonusThreshold = 100,
                AdditionalBonusDiscountThresholdQuantities = [],
            },
        };

        await BuyAsync(SOLD, 13, config);

        Granted().Should().Be(15, "two whole bundles of six");
        Charged().Should().Be(65);
    }

    [Theory]
    [InlineData(SOLD, 101)]
    [InlineData(UNBUNDLED, 2)]
    public async Task more_than_the_spinner_allows_is_refused_before_charging(
        int offerId,
        int quantity
    )
    {
        var buy = () => BuyAsync(offerId, quantity);

        (await buy.Should().ThrowAsync<CatalogPurchaseException>())
            .Which.ErrorType.Should()
            .Be(CatalogPurchaseErrorType.PurchaseFailed);
        _catalog.Fakes.Log.Of("TryDebitAsync").Should().BeEmpty("nothing is charged");
        _catalog.Fakes.Log.Of("GrantCatalogOfferAsync").Should().BeEmpty();
    }

    private int Granted() => (int)_catalog.Fakes.Log.Of("GrantCatalogOfferAsync").Single().Args[2]!;

    private int Charged() =>
        (
            (IEnumerable<WalletDebitRequest>)
                _catalog.Fakes.Log.Of("TryDebitAsync").Single().Args[0]!
        ).Sum(x => x.Amount);

    /// <summary>The buyer's purchase grain buying an offer from the catalog as published.</summary>
    private async Task BuyAsync(int offerId, int quantity, CatalogConfig? config = null)
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

        if (config is not null)
            Set(grain, "_catalogConfig", config);
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
            string.Empty,
            quantity,
            Ct
        );
    }

    private static void Set(object grain, string field, object value) =>
        grain
            .GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(grain, value);
}
