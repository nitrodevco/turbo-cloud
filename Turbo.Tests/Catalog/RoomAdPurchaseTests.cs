using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Turbo.Catalog;
using Turbo.Database.Entities.Catalog;
using Turbo.Messages.Registry;
using Turbo.Navigator;
using Turbo.Navigator.Configuration;
using Turbo.PacketHandlers.Catalog;
using Turbo.Players.Configuration;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// Buying a room ad (the shop's Room event page, "Promote Room"). Habbo's page sends category -1
/// unless the buyer picks one (RoomAdsCatalogWidget writes the drop menu's first selection before
/// its purchase data exists), the page is told whether the buyer is a club member (official
/// client capture: isVip false without club), and the offer "room_ad_plus_badge" carries a badge.
/// </summary>
public sealed class RoomAdPurchaseTests : IDisposable
{
    private const int AD_OFFER = 310;
    private const int ROOM = 11;

    private static readonly ImmutableArray<NavigatorEventCategorySnapshot> Categories =
    [
        new()
        {
            Id = 1,
            Name = "Hottest Events",
            Visible = false,
        },
        new()
        {
            Id = 2,
            Name = "Parties & Music",
            Visible = true,
        },
        new()
        {
            Id = 5,
            Name = "Trading",
            Visible = true,
        },
    ];

    private readonly CatalogFixture _catalog = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _catalog.Dispose();

    [Theory]
    [InlineData(5, 5)]
    [InlineData(-1, 2)]
    [InlineData(1, 2)]
    [InlineData(99, 2)]
    public void TheAdsCategory_IsTheOneAskedForWhenShown_ElseTheFirstShown(
        int requested,
        int expected
    )
    {
        _fakes.Handlers["GetEventCategories"] = _ => Categories;

        Navigator().GetRoomAdCategoryId(requested).Should().Be(expected);
    }

    [Fact]
    public void WithNoCategoryShown_NoAdCategoryIsFound()
    {
        _fakes.Handlers["GetEventCategories"] = _ =>
            (ImmutableArray<NavigatorEventCategorySnapshot>)[Categories[0]];

        Navigator().GetRoomAdCategoryId(-1).Should().BeNull();
    }

    [Fact]
    public async Task AnAdBoughtWithTheUntouchedCategoryMenu_IsBoughtIntoTheFirstShownCategory()
    {
        _fakes.Handlers["GetEventCategories"] = _ => Categories;

        await new PurchaseRoomAdMessageMessageHandler(
            Navigator(),
            _fakes.Create<IGrainFactory>()
        ).HandleAsync(
            new PurchaseRoomAdMessageMessage
            {
                PageId = 245,
                OfferId = AD_OFFER,
                FlatId = ROOM,
                Name = "Party at agent5",
                Extended = false,
                Description = "Come and dance",
                CategoryId = -1,
            },
            new MessageContext(_fakes.Create<ISessionContext>(), 1, 7),
            Ct
        );

        _fakes
            .Log.Of("PurchaseRoomAdAsync")
            .Should()
            .ContainSingle("the purchase is not refused")
            .Which.Args[2]
            .Should()
            .Be(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ThePurchaseInfo_SaysWhetherTheBuyerIsAClubMember(bool member)
    {
        _fakes.Handlers["HasActiveAsync"] = _ => Task.FromResult(member);
        _fakes.Handlers["SearchRoomsAsync"] = _ =>
            Task.FromResult<ImmutableArray<NavigatorSearchResultSnapshot>>([OwnRoom()]);

        await new GetRoomAdPurchaseInfoMessageHandler(
            _fakes.Create<INavigatorService>(),
            _fakes.Create<IGrainFactory>()
        ).HandleAsync(
            new GetRoomAdPurchaseInfoMessage(),
            new MessageContext(_fakes.Create<ISessionContext>(), 1, 7),
            Ct
        );

        var info = _fakes
            .Log.Of("SendComposerAsync")
            .Select(x => x.Args[0])
            .OfType<RoomAdPurchaseInfoEventMessageComposer>()
            .Should()
            .ContainSingle()
            .Subject;

        info.IsVip.Should().Be(member);
        info.Rooms.Should().ContainSingle().Which.RoomId.Should().Be(ROOM);
    }

    [Fact]
    public async Task AnAdBought_GivesTheOffersBadge()
    {
        _catalog.Db.Insert(
            new CatalogOfferEntity
            {
                Id = AD_OFFER,
                CatalogPageEntityId = FURNITURE,
                LocalizationId = "room_ad_plus_badge",
                CostCredits = 0,
                CostCurrency = 15,
                CanGift = false,
                CanBundle = false,
                ClubLevel = 0,
                Visible = true,
                Page = null!,
            }
        );
        _catalog.Db.Insert(
            new CatalogProductEntity
            {
                Id = AD_OFFER,
                CatalogOfferEntityId = AD_OFFER,
                ProductType = ProductType.Badge,
                FurnitureDefinitionEntityId = null,
                ExtraParam = "RADZZ",
                Quantity = 1,
                Offer = null!,
            }
        );

        var provider = _catalog.NormalProvider();

        await provider.ReloadAsync(Ct);

        var fakes = _catalog.Fakes;

        fakes.Handlers["GetCatalogSnapshot"] = _ => provider.Current;
        fakes.Handlers["TryDebitAsync"] = _ => Task.FromResult(WalletDebitResult.Success());
        fakes.Handlers["GetControllerLevelAsync"] = _ => Task.FromResult(RoomControllerType.Owner);
        fakes.Handlers["CreateEventAsync"] = _ =>
            Task.FromResult<RoomEventSnapshot?>(
                new RoomEventSnapshot
                {
                    EventId = 1,
                    RoomId = ROOM,
                    OwnerId = 7,
                    OwnerName = "agent5",
                    CategoryId = 2,
                    Name = "Party at agent5",
                    Description = "Come and dance",
                    CreatedAtUtc = DateTime.UtcNow,
                    ExpiresAtUtc = DateTime.UtcNow.AddMinutes(120),
                }
            );

        var grain = GrainHarness.Create(
            typeof(CatalogModule).Assembly,
            "Turbo.Catalog.Grains.CatalogPurchaseGrain",
            fakes,
            _catalog.Db,
            playerId: 7
        );

        grain
            .GetType()
            .GetField("_catalogService", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(grain, fakes.Create<ICatalogService>());
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

        await ((ICatalogPurchaseGrain)grain).PurchaseRoomAdAsync(
            AD_OFFER,
            ROOM,
            2,
            "Party at agent5",
            "Come and dance",
            false,
            TimeSpan.FromMinutes(120),
            Ct
        );

        fakes.Log.Of("TryDebitAsync").Should().ContainSingle("the ad is paid once");
        fakes.Log.Of("GiveBadgeAsync").Should().ContainSingle().Which.Args[0].Should().Be("RADZZ");
    }

    private NavigatorService Navigator() =>
        new(
            NullLogger<INavigatorService>.Instance,
            _fakes.Create<INavigatorProvider>(),
            _fakes.Create<IGrainFactory>(),
            Options.Create(new NavigatorConfig()),
            Options.Create(new PlayerNavigatorConfig()),
            _fakes.Create<IPermissionRegistryProvider>(),
            _fakes.Create<IWordFilter>()
        );

    private static NavigatorSearchResultSnapshot OwnRoom() =>
        (
            (NavigatorSearchResultSnapshot)
                RuntimeHelpers.GetUninitializedObject(typeof(NavigatorSearchResultSnapshot))
        ) with
        {
            RoomId = ROOM,
            Name = "agent5 room",
        };
}
