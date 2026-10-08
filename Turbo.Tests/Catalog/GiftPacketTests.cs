using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Catalog;
using Turbo.Catalog.Configuration;
using Turbo.Catalog.Exceptions;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Extensions;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Catalog;

/// <summary>
/// The gift messages as the client writes and reads them (its <c>GiftWrappingConfigurationParser</c>
/// and <c>PurchaseFromCatalogAsGiftMessageComposer</c>), the wrapping read from config, and how a
/// refused gift is told to the gift dialog.
/// </summary>
public sealed class GiftPacketTests
{
    [Fact]
    public void TheWrappingWritesPriceThenFourIdLists()
    {
        var packet = PacketHarness.Encode(
            new GiftWrappingConfigurationEventMessageComposer
            {
                Wrapping = new GiftWrappingSnapshot
                {
                    Enabled = true,
                    Price = 2,
                    StuffTypes = [3372, 3373],
                    BoxTypes = [0, 8],
                    RibbonTypes = [10],
                    DefaultStuffTypes = [187],
                },
            }
        );

        packet
            .Header.Should()
            .Be(PacketHarness.Outgoing("GiftWrappingConfigurationMessageComposer"));
        packet.PopBoolean().Should().BeTrue();
        packet.PopInt().Should().Be(2);
        ReadInts(packet).Should().Equal(3372, 3373);
        ReadInts(packet).Should().Equal(0, 8);
        ReadInts(packet).Should().Equal(10);
        ReadInts(packet).Should().Equal(187);
        packet.Remaining.Should().Be(0);
    }

    [Fact]
    public async Task AGift_ReachesTheBuyersPurchaseGrain_AsTheDialogSentIt()
    {
        var harness = new PacketHarness();

        harness.Fakes.Handlers[nameof(ICatalogPurchaseGrain.PurchaseOfferAsGiftAsync)] = _ =>
            Task.FromResult(Offer());

        var replies = await SendGiftAsync(harness);

        replies
            .Should()
            .ContainSingle()
            .Which.Header.Should()
            .Be(PacketHarness.Outgoing("PurchaseOKMessageComposer"));

        var request = (CatalogGiftRequest)
            harness
                .Fakes.Log.Of(nameof(ICatalogPurchaseGrain.PurchaseOfferAsGiftAsync))
                .Should()
                .ContainSingle()
                .Which.Args[0]!;

        request
            .Should()
            .Be(
                new CatalogGiftRequest
                {
                    OfferId = 55,
                    ExtraParam = "x",
                    ReceiverName = "friend",
                    Message = "hi",
                    SpriteId = 3372,
                    BoxType = 3,
                    RibbonType = 5,
                    ShowPurchaserName = true,
                }
            );
    }

    [Fact]
    public async Task AReceiverNotFound_IsTheDialogsOwnAlert()
    {
        var harness = new PacketHarness();

        harness.Fakes.Handlers[nameof(ICatalogPurchaseGrain.PurchaseOfferAsGiftAsync)] = _ =>
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.ReceiverNotFound);

        var replies = await SendGiftAsync(harness);

        replies
            .Should()
            .ContainSingle()
            .Which.Header.Should()
            .Be(PacketHarness.Outgoing("GiftReceiverNotFoundMessageComposer"));
    }

    [Fact]
    public async Task AReceiverWhoBlockedTheBuyer_IsAPurchaseError()
    {
        var harness = new PacketHarness();

        harness.Fakes.Handlers[nameof(ICatalogPurchaseGrain.PurchaseOfferAsGiftAsync)] = _ =>
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.BlockedByReceiver);

        var reply = (await SendGiftAsync(harness)).Should().ContainSingle().Subject;

        reply.Header.Should().Be(PacketHarness.Outgoing("PurchaseErrorMessageComposer"));
        reply.PopInt().Should().Be((int)CatalogPurchaseErrorType.BlockedByReceiver);
    }

    [Fact]
    public void TheWrapping_OffersTheConfiguredPresentsBySprite_LeavingOutUnknownOnes()
    {
        var fakes = new Fakes();

        fakes.Handlers["TryGetDefinitionByName"] = call =>
            (string)call.Args[0]! switch
            {
                "present_wrap*1" => Definition(1791, 3372),
                "present_gen" => Definition(167, 187),
                _ => null,
            };

        var provider = (IGiftWrappingProvider)
            Activator.CreateInstance(
                typeof(CatalogModule).Assembly.GetType(
                    "Turbo.Catalog.Providers.GiftWrappingProvider"
                )!,
                Options.Create(
                    new CatalogConfig
                    {
                        GiftWrapping = new GiftWrappingConfig
                        {
                            Price = 4,
                            WrapperNames = ["present_wrap*1", "present_wrap*99"],
                            DefaultNames = ["present_gen"],
                        },
                    }
                ),
                fakes.Create<IFurnitureDefinitionProvider>(),
                NullLogger<IGiftWrappingProvider>.Instance
            )!;

        var wrapping = provider.GetWrapping();

        wrapping.Price.Should().Be(4);
        wrapping.StuffTypes.Should().Equal(3372);
        wrapping.DefaultStuffTypes.Should().Equal(187);
        wrapping.BoxTypes.Should().Equal(0, 1, 2, 3, 4, 5, 6, 8);
        wrapping.RibbonTypes.Should().HaveCount(11);
    }

    private static Task<List<Turbo.Primitives.Packets.ClientPacket>> SendGiftAsync(
        PacketHarness harness
    ) =>
        harness.SendAsync(
            PacketHarness.Incoming("PurchaseFromCatalogAsGiftMessageEvent"),
            PacketHarness.Payload(w =>
                w.Int(1)
                    .Int(55)
                    .String("x")
                    .String("friend")
                    .String("hi")
                    .Int(3372)
                    .Int(3)
                    .Int(5)
                    .Bool(true)
            ),
            playerId: 7
        );

    private static CatalogOfferSnapshot Offer() =>
        new()
        {
            Id = 55,
            PageId = 1,
            LocalizationId = "offer",
            Rentable = false,
            CostCredits = 5,
            CostSilver = 0,
            CostCurrency = 0,
            ActivityPointType = null,
            CanGift = true,
            CanBundle = true,
            ClubLevel = 0,
            Visible = true,
            ProductIds = [],
            Products = [],
        };

    private static int[] ReadInts(Turbo.Primitives.Packets.ClientPacket packet)
    {
        var values = new int[packet.PopInt()];

        for (var i = 0; i < values.Length; i++)
            values[i] = packet.PopInt();

        return values;
    }

    private static Turbo.Primitives.Furniture.Snapshots.FurnitureDefinitionSnapshot Definition(
        int id,
        int spriteId
    ) =>
        new FurnitureDefinitionEntity
        {
            Id = id,
            SpriteId = spriteId,
            Name = "present",
            ProductType = ProductType.Floor,
            FurniCategory = FurnitureCategory.Default,
            Logic = "present",
            Width = 1,
            Length = 1,
            StackHeight = 1,
            CanStack = true,
            CanWalk = false,
            CanSit = false,
            CanLay = false,
            CanRecycle = true,
            CanTrade = true,
            CanGroup = true,
            CanSell = true,
        }.ToSnapshot(0.01);
}
