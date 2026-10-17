using System.Collections.Immutable;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Turbo.Catalog;
using Turbo.Database.Entities.Catalog;
using Turbo.Players.Configuration;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms.Object;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// The purchase confirmation as the official server writes it: the offer as bought, an offer
/// priced in credits alone with activity-point type 0, then the furni the purchase created, floor
/// and wall apart. The expected bytes are two purchases recorded from the official client.
/// </summary>
public sealed class PurchaseOkTests : IDisposable
{
    private const int CLUB_OFFER = 200;

    private readonly CatalogFixture _catalog = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _catalog.Dispose();

    [Fact]
    public void ACreditFurniPurchase_IsTheOfficialBytes()
    {
        var packet = PacketHarness.Encode(
            new PurchaseOKMessageComposer
            {
                Offer = CoinOffer(1198, "a0 CF_1_coin_bronze", credits: 2, diamonds: 0),
                FloorItemIds = [RoomObjectId.Parse(924563480)],
            }
        );

        Convert
            .ToHexString(packet.PopBytes(packet.Remaining))
            .Should()
            .Be(
                "000004AE001361302043465F315F636F696E5F62726F6E7A65000000000200000000000000000000"
                    + "000001000173000008100001310000000100000000000000000001371BB81800000000"
            );
    }

    [Fact]
    public void ADiamondFurniPurchase_IsTheOfficialBytes()
    {
        var packet = PacketHarness.Encode(
            new PurchaseOKMessageComposer
            {
                Offer = CoinOffer(18629, "a0 CF_1_coin_bronze_d", credits: 1, diamonds: 1),
                FloorItemIds = [RoomObjectId.Parse(924563482)],
            }
        );

        Convert
            .ToHexString(packet.PopBytes(packet.Remaining))
            .Should()
            .Be(
                "000048C5001561302043465F315F636F696E5F62726F6E7A655F6400000000010000000100000005"
                    + "0000000001000173000008100001310000000100000000000000000001371BB81A00000000"
            );
    }

    [Fact]
    public async Task APurchase_ConfirmsTheFurniThePurchaseGrainReturned()
    {
        var provider = _catalog.NormalProvider();

        await provider.ReloadAsync(Ct);

        var harness = new PacketHarness();

        harness.Fakes.Handlers["GetCatalogSnapshot"] = _ => provider.Current;
        harness.Fakes.Handlers[nameof(ICatalogPurchaseGrain.PurchaseOfferFromCatalogAsync)] = _ =>
            Task.FromResult(
                new CatalogPurchaseResult
                {
                    Offer = provider.Current.OffersById[SOLD],
                    FloorItemIds = [RoomObjectId.Parse(41), RoomObjectId.Parse(42)],
                    WallItemIds = [RoomObjectId.Parse(43)],
                }
            );
        harness.Resolver.Overrides[typeof(ICatalogService)] =
            harness.Fakes.Create<ICatalogService>();

        var reply = (
            await harness.SendAsync(
                PacketHarness.Incoming("PurchaseFromCatalogMessageEvent"),
                PacketHarness.Payload(w => w.Int(FURNITURE).Int(SOLD).String("").Int(1)),
                playerId: 7
            )
        ).Single(x => x.Header == PacketHarness.Outgoing("PurchaseOKMessageComposer"));

        reply.PopInt().Should().Be(SOLD);
        reply.PopString();
        reply.PopBoolean();
        reply.PopInt();
        reply.PopInt();
        reply.PopInt();
        reply.PopBoolean();
        reply.PopInt().Should().Be(1, "one product");
        reply.PopString().Should().Be("s");
        reply.PopInt();
        reply.PopString();
        reply.PopInt();
        reply.PopBoolean();
        reply.PopInt();
        reply.PopBoolean();
        ReadInts(reply).Should().Equal(41, 42);
        ReadInts(reply).Should().Equal(43);
        reply.Remaining.Should().Be(0);
    }

    [Fact]
    public async Task ThePurchaseGrain_ListsTheFurniGiven_FloorAndWallApart()
    {
        var provider = _catalog.NormalProvider();

        await provider.ReloadAsync(Ct);

        var fakes = _catalog.Fakes;

        fakes.Handlers["GetCatalogSnapshot"] = _ => provider.Current;
        fakes.Handlers["TryDebitAsync"] = _ => Task.FromResult(WalletDebitResult.Success());
        fakes.Handlers["GrantCatalogOfferAsync"] = _ =>
            Task.FromResult<ImmutableArray<FurnitureItemSnapshot>>([
                Item(51, CHAIR),
                Item(52, POSTER),
                Item(53, CHAIR),
            ]);

        var purchase = await PurchaseGrain()
            .PurchaseOfferFromCatalogAsync(CatalogType.Normal, SOLD, string.Empty, 1, Ct);

        purchase.Offer.Id.Should().Be(SOLD);
        purchase.FloorItemIds.Select(x => x.Value).Should().Equal(51, 53);
        purchase.WallItemIds.Select(x => x.Value).Should().Equal(52);
    }

    [Fact]
    public async Task AClubOfferInCreditsAlone_HasActivityPointTypeZero()
    {
        _catalog.Db.Insert(
            new CatalogOfferEntity
            {
                Id = CLUB_OFFER,
                CatalogPageEntityId = FURNITURE,
                LocalizationId = "HABBO_CLUB_VIP_3_DAYS",
                CostCredits = 10000,
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
                Id = CLUB_OFFER,
                CatalogOfferEntityId = CLUB_OFFER,
                ProductType = ProductType.Floor,
                FurnitureDefinitionEntityId = CHAIR,
                SubscriptionType = SubscriptionType.HabboClub,
                SubscriptionDays = 3,
                Quantity = 1,
                Offer = null!,
            }
        );

        var normal = _catalog.NormalProvider();

        await normal.ReloadAsync(Ct);

        var service = new CatalogService(
            NullLogger<ICatalogService>.Instance,
            normal,
            _catalog.BuildersClubProvider(),
            _catalog.Db,
            Options.Create(new PlayerConfig())
        );

        service
            .GetClubOffers()
            .Single(x => x.OfferId == CLUB_OFFER)
            .PriceActivityPointType.Should()
            .Be(0);
    }

    private ICatalogPurchaseGrain PurchaseGrain()
    {
        var fakes = _catalog.Fakes;
        var grain = GrainHarness.Create(
            typeof(CatalogModule).Assembly,
            "Turbo.Catalog.Grains.CatalogPurchaseGrain",
            fakes,
            _catalog.Db,
            playerId: 7
        );

        Set(grain, "_catalogService", fakes.Create<ICatalogService>());
        Set(grain, "_definitionProvider", _catalog.Definitions);
        // Keyed by the buyer, as Orleans keys it: the grain reads who is buying from its key.
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

        return (ICatalogPurchaseGrain)grain;
    }

    private FurnitureItemSnapshot Item(int id, int definitionId)
    {
        var definition = _catalog.Definitions.TryGetDefinition(definitionId)!;

        return new FurnitureItemSnapshot
        {
            ItemId = id,
            SpriteId = definition.SpriteId,
            OwnerId = 7,
            OwnerName = "buyer",
            Definition = definition,
            StuffData = new LegacyStuffSnapshot { StuffBitmask = 0, Data = "0" },
            ExtraData = string.Empty,
            SecondsToExpiration = -1,
            HasRentPeriodStarted = false,
            RoomId = -1,
        };
    }

    /// <summary>A bronze coin as the official catalogue sells it, for credits and maybe diamonds.</summary>
    private static CatalogOfferSnapshot CoinOffer(
        int id,
        string localizationId,
        int credits,
        int diamonds
    )
    {
        var product = new CatalogProductSnapshot
        {
            Id = id,
            OfferId = id,
            ProductType = ProductType.Floor,
            FurniDefinitionId = 2064,
            SpriteId = 2064,
            ExtraParam = "1",
            Quantity = 1,
            UniqueSize = 0,
            UniqueRemaining = 0,
            ClassName = null,
        };

        return new CatalogOfferSnapshot
        {
            Id = id,
            PageId = 2129114,
            LocalizationId = localizationId,
            Rentable = false,
            CostCredits = credits,
            CostSilver = 0,
            CostCurrency = diamonds,
            ActivityPointType = diamonds > 0 ? 5 : null,
            CanGift = false,
            CanBundle = false,
            ClubLevel = 0,
            Visible = true,
            ProductIds = [product.Id],
            Products = [product],
        };
    }

    private static int[] ReadInts(Turbo.Primitives.Packets.ClientPacket packet)
    {
        var values = new int[packet.PopInt()];

        for (var i = 0; i < values.Length; i++)
            values[i] = packet.PopInt();

        return values;
    }

    private static void Set(object grain, string field, object value) =>
        grain
            .GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(grain, value);
}
