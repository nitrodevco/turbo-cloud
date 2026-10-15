using System.Collections.Immutable;
using FluentAssertions;
using Orleans;
using Turbo.Catalog;
using Turbo.Catalog.Configuration;
using Turbo.Database.Entities.Players;
using Turbo.PacketHandlers.Catalog;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Catalog.TargetedOffers;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Catalog;

/// <summary>
/// Targeted offers as Flash's <c>catalog/targetedoffers/OfferController</c> reads them
/// (WIN63-202610131318, <c>offers.enabled=true</c> on Habbo): the client asks
/// <c>GetNextTargetedOffer</c> once at login, and Habbo answered an empty
/// <c>TargetedOfferNotFound</c> on every recorded login (official client capture, 2026-10-12, 4
/// logins). A running offer arrives as <c>TargetedOffer</c> (<c>TargetedOfferData.parse</c>),
/// opening its dialog, or docking in the toolbar when the player left it minimized (state 4,
/// <c>SetTargetedOfferState</c>). The four handlers were stubs.
/// </summary>
public sealed class PlayerTargetedOfferTests : IDisposable
{
    private const int PLAYER = 1;
    private const int OFFER = 7;
    private const int CATALOG_OFFER = 500;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly TargetedOfferConfig _config = new();
    private readonly ManualTimeProvider _time = new(
        new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero)
    );

    public PlayerTargetedOfferTests()
    {
        _fakes.Handlers["TryDebitAsync"] = _ => Task.FromResult(WalletDebitResult.Success());
        _fakes.Handlers["GetCatalogSnapshot"] = _ =>
            CatalogSnapshot.Empty with
            {
                OffersById = ImmutableDictionary<int, CatalogOfferSnapshot>.Empty.Add(
                    CATALOG_OFFER,
                    CatalogOffer()
                ),
            };
        _db.Insert(
            new PlayerEntity
            {
                Id = PLAYER,
                Name = "offers",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task With_no_offer_the_answer_is_an_empty_not_found()
    {
        await (await GrainAsync()).SendNextOfferAsync(Ct);

        var sent = Sent<IComposer>().Should().ContainSingle().Subject;

        sent.Should().BeOfType<TargetedOfferNotFoundEventMessageComposer>();
        Bytes(sent).Should().BeEmpty();
    }

    [Fact]
    public async Task The_handler_asks_the_players_grain()
    {
        await new GetNextTargetedOfferMessageHandler(_fakes.Create<IGrainFactory>()).HandleAsync(
            new GetNextTargetedOfferMessage(),
            new(_fakes.Create<ISessionContext>(), PLAYER, -1),
            Ct
        );

        _fakes.Log.Of("SendNextOfferAsync").Should().ContainSingle();
    }

    [Fact]
    public async Task A_running_offer_is_sent_in_TargetedOfferData_order()
    {
        _config.Offers.Add(Offer(endsAt: _time.GetUtcNow().UtcDateTime.AddHours(1)));

        await (await GrainAsync()).SendNextOfferAsync(Ct);

        var offer = Sent<TargetedOfferEventMessageComposer>().Single();
        var packet = PacketHarness.Encode(offer);

        packet.PopInt().Should().Be(TargetedOfferTrackingStates.NEW);
        packet.PopInt().Should().Be(OFFER);
        packet.PopString().Should().Be("offer_code");
        packet.PopString().Should().Be("product_code");
        packet.PopInt().Should().Be(25);
        packet.PopInt().Should().Be(10);
        packet.PopInt().Should().Be(5);
        packet.PopInt().Should().Be(3);
        packet.PopInt().Should().Be(3600);
        packet.PopString().Should().Be("Title");
        packet.PopString().Should().Be("Description");
        packet.PopString().Should().Be("image.png");
        packet.PopString().Should().Be("icon.png");
        packet.PopInt().Should().Be(1);
        packet.PopInt().Should().Be(1);
        packet.PopString().Should().Be("sub_code");
        packet.Remaining.Should().Be(0);
    }

    [Theory]
    [InlineData(-2, null, true)]
    [InlineData(1, null, false)]
    [InlineData(null, -1, false)]
    public async Task Only_a_running_offer_is_offered(
        int? startsInHours,
        int? endsInHours,
        bool sent
    )
    {
        var now = _time.GetUtcNow().UtcDateTime;

        _config.Offers.Add(
            Offer(
                startsAt: startsInHours is { } s ? now.AddHours(s) : null,
                endsAt: endsInHours is { } e ? now.AddHours(e) : null
            )
        );

        await (await GrainAsync()).SendNextOfferAsync(Ct);

        Sent<TargetedOfferEventMessageComposer>().Any().Should().Be(sent);
    }

    [Fact]
    public async Task A_minimized_offer_comes_back_minimized()
    {
        _config.Offers.Add(Offer());

        await (await GrainAsync()).SetTrackingStateAsync(
            OFFER,
            TargetedOfferTrackingStates.MINIMIZED,
            Ct
        );

        // The next login: a new activation reads the saved state.
        await (await GrainAsync()).SendNextOfferAsync(Ct);

        Sent<TargetedOfferEventMessageComposer>()
            .Single()
            .Offer.TrackingState.Should()
            .Be(TargetedOfferTrackingStates.MINIMIZED);
    }

    [Fact]
    public async Task A_purchase_takes_the_price_times_the_quantity_and_grants_the_catalogue_offer()
    {
        _config.Offers.Add(Offer());

        var grain = await GrainAsync();

        (await grain.PurchaseAsync(OFFER, 2, Ct)).Should().BeTrue();

        var debit = (List<WalletDebitRequest>)_fakes.Log.Of("TryDebitAsync").Single().Args[0];

        debit
            .Select(x => (x.CurrencyKind, x.Amount))
            .Should()
            .Equal((CurrencyKind.Credits, 50), (CurrencyKind.ActivityPoints(5), 20));

        var grant = _fakes.Log.Of("GrantCatalogOfferAsync").Single();

        ((CatalogOfferSnapshot)grant.Args[0]).Id.Should().Be(CATALOG_OFFER);
        grant.Args[2].Should().Be(2);

        // One left of three: the next login offers it with a limit of 1.
        await (await GrainAsync()).SendNextOfferAsync(Ct);

        Sent<TargetedOfferEventMessageComposer>().Single().Offer.PurchaseLimit.Should().Be(1);
    }

    [Fact]
    public async Task A_purchase_over_the_limit_takes_nothing()
    {
        _config.Offers.Add(Offer());

        var grain = await GrainAsync();

        (await grain.PurchaseAsync(OFFER, 4, Ct)).Should().BeFalse();

        _fakes.Log.Of("TryDebitAsync").Should().BeEmpty();
        _fakes.Log.Of("GrantCatalogOfferAsync").Should().BeEmpty();
    }

    [Fact]
    public async Task A_short_balance_grants_nothing()
    {
        _config.Offers.Add(Offer());
        _fakes.Handlers["TryDebitAsync"] = _ =>
            Task.FromResult(
                WalletDebitResult.InsufficientBalance(
                    new WalletDebitFailure { CurrencyKind = CurrencyKind.Credits, Amount = 25 }
                )
            );

        (await (await GrainAsync()).PurchaseAsync(OFFER, 1, Ct)).Should().BeFalse();

        _fakes.Log.Of("GrantCatalogOfferAsync").Should().BeEmpty();
    }

    [Fact]
    public async Task A_bought_out_offer_is_no_longer_offered()
    {
        _config.Offers.Add(Offer());

        (await (await GrainAsync()).PurchaseAsync(OFFER, 3, Ct)).Should().BeTrue();

        await (await GrainAsync()).SendNextOfferAsync(Ct);

        Sent<IComposer>()
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<TargetedOfferNotFoundEventMessageComposer>();
    }

    private TargetedOfferDefinition Offer(DateTime? startsAt = null, DateTime? endsAt = null) =>
        new()
        {
            Id = OFFER,
            Identifier = "offer_code",
            CatalogOfferId = CATALOG_OFFER,
            ProductCode = "product_code",
            SubProductCodes = ["sub_code"],
            Title = "Title",
            Description = "Description",
            ImageUrl = "image.png",
            IconImageUrl = "icon.png",
            Type = 1,
            PriceInCredits = 25,
            PriceInActivityPoints = 10,
            ActivityPointType = 5,
            PurchaseLimit = 3,
            StartsAt = startsAt,
            EndsAt = endsAt,
        };

    private static CatalogOfferSnapshot CatalogOffer() =>
        new()
        {
            Id = CATALOG_OFFER,
            PageId = 1,
            LocalizationId = "product_code",
            Rentable = false,
            CostCredits = 0,
            CostSilver = 0,
            CostCurrency = 0,
            ActivityPointType = null,
            CanGift = false,
            CanBundle = false,
            ClubLevel = 0,
            Visible = true,
            ProductIds = [],
            Products = [],
        };

    private async Task<IPlayerTargetedOfferGrain> GrainAsync()
    {
        var grain = GrainHarness.Create(
            typeof(CatalogModule).Assembly,
            "Turbo.Catalog.Grains.PlayerTargetedOfferGrain",
            _fakes,
            _db,
            playerId: PLAYER
        );
        RoomHarness.SetField(grain, "_config", _config);
        RoomHarness.SetField(grain, "_time", _time);
        RoomHarness.SetField(grain, "_catalogService", _fakes.Create<ICatalogService>());
        await ((Grain)grain).OnActivateAsync(Ct);

        return (IPlayerTargetedOfferGrain)grain;
    }

    private static byte[] Bytes(IComposer composer)
    {
        var packet = PacketHarness.Encode(composer);

        return packet.PopBytes(packet.Remaining);
    }

    private IEnumerable<T> Sent<T>()
        where T : IComposer =>
        _fakes
            .Log.Of("SendComposerAsync")
            .SelectMany(x =>
                x.Args[0] switch
                {
                    IReadOnlyList<IComposer> many => many,
                    IComposer one => [one],
                    _ => [],
                }
            )
            .OfType<T>();
}
