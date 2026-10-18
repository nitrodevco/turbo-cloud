using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Catalog.Configuration;
using Turbo.Catalog.Marketplace;
using Turbo.Catalog.Vault;
using Turbo.Database.Configuration;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Movements;
using Turbo.Database.Entities.Players;
using Turbo.Database.Movements;
using Turbo.Primitives.Catalog.Marketplace;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory.Factories;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Incoming.Vault;
using Turbo.Primitives.Messages.Outgoing.Inventory.Furni;
using Turbo.Primitives.Messages.Outgoing.Marketplace;
using Turbo.Primitives.Messages.Outgoing.Vault;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Vault.Enums;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// The marketplace as players use it: an offered item leaves the seller's inventory and is listed
/// one row per kind (the cheapest offer, how many there are), a buyer pays the price and gets the
/// item, the seller's revenue (Flash's MarketplaceView.calculateFinalPrice) waits until redeemed,
/// a lost race offers the next cheapest, an offer taken back returns its item, and the item
/// stats answer the price history. The packets are written byte for byte as the official client
/// capture of 2026-10-13 shows them.
/// </summary>
public sealed class MarketplaceTests : IDisposable
{
    private const int SELLER = 7;
    private const int BUYER = 8;
    private const int OTHER = 9;
    private const int LAMP = 20;
    private const int COIN = 21;
    private const int BED = 22;

    private static readonly DateTime NOW = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

    private readonly CatalogFixture _catalog = new();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(NOW));
    private readonly MarketplaceService _marketplace;

    /// <summary>Each player's inventory: item id to (definition, extra data).</summary>
    private readonly Dictionary<
        long,
        Dictionary<int, (int Definition, string Extra)>
    > _inventories = [];
    private readonly Dictionary<long, int> _credits = new()
    {
        [SELLER] = 0,
        [BUYER] = 1000,
        [OTHER] = 1000,
    };
    private int _nextItemId = 500;

    /// <summary>The causes the inventories were handed: what was used up, and what was given.</summary>
    private readonly List<MovementCause> _consumeCauses = [];
    private readonly List<MovementCause> _grantCauses = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public MarketplaceTests()
    {
        foreach (var (id, name) in new[] { (SELLER, "seller"), (BUYER, "buyer"), (OTHER, "other") })
        {
            _catalog.Db.Insert(
                new PlayerEntity
                {
                    Id = id,
                    Name = name,
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                    PlayerStatus = PlayerStatusType.Offline,
                }
            );
            _inventories[id] = [];
        }

        _catalog.AddDefinition(LAMP, "rare_lamp");

        var fakes = _catalog.Fakes;
        var definitions = _catalog.Definitions;

        fakes.Handlers["TryGetDefinitionBySprite"] = call =>
            definitions.TryGetDefinition((int)call.Args[1]!);
        fakes.Handlers["HasAsync"] = _ => Task.FromResult(true);
        fakes.Handlers["GetItemSnapshotsAsync"] = call =>
        {
            var inventory = _inventories[(long)call.Key!];

            return Task.FromResult(
                ((ImmutableArray<RoomObjectId>)call.Args[0]!)
                    .Where(x => inventory.ContainsKey(x.Value))
                    .Select(x => Snapshot(x.Value, inventory[x.Value]))
                    .ToImmutableArray()
            );
        };
        fakes.Handlers["ConsumeFurnitureAsync"] = call =>
        {
            var inventory = _inventories[(long)call.Key!];
            var id = ((RoomObjectId)call.Args[0]!).Value;

            _consumeCauses.Add((MovementCause)call.Args[1]!);

            return Task.FromResult(inventory.Remove(id, out var item) ? Snapshot(id, item) : null);
        };
        fakes.Handlers["GrantFurnitureAsync"] = call =>
        {
            var id = _nextItemId++;
            var item = ((int)call.Args[0]!, (string?)call.Args[1] ?? "");

            _grantCauses.Add((MovementCause)call.Args[2]!);
            _inventories[(long)call.Key!][id] = item;

            return Task.FromResult<FurnitureItemSnapshot?>(Snapshot(id, item));
        };
        fakes.Handlers["TryDebitAsync"] = call =>
        {
            var amount = ((List<WalletDebitRequest>)call.Args[0]!).Sum(x => x.Amount);
            var player = (long)call.Key!;

            if (_credits[player] < amount)
                return Task.FromResult(
                    WalletDebitResult.InsufficientBalance(
                        new WalletDebitFailure
                        {
                            CurrencyKind = CurrencyKind.Credits,
                            Amount = amount,
                        }
                    )
                );

            _credits[player] -= amount;

            return Task.FromResult(WalletDebitResult.Success());
        };
        fakes.Handlers["CreditAsync"] = call =>
        {
            _credits[(long)call.Key!] += (int)call.Args[1]!;

            return Task.FromResult(true);
        };
        fakes.Handlers["Create"] = call =>
            fakes.Create<IFurnitureItem>((call.Args[3], call.Args[4]));
        fakes.Handlers["GetSnapshot"] = call =>
        {
            var (definition, extra) = ((object?, object?))call.Key!;

            return Snapshot(
                0,
                (((FurnitureDefinitionSnapshot)definition!).Id, (string?)extra ?? "")
            );
        };

        _marketplace = NewMarketplace(fakes.Create<IAssetMovementRecorder>());
    }

    public void Dispose() => _catalog.Dispose();

    [Fact]
    public async Task A_listing_and_the_sale_it_ends_in_name_the_same_offer()
    {
        var marketplace = NewMarketplace(await RecordingAsync());
        var chair = Give(SELLER, CHAIR, "state:1");

        await marketplace.MakeOfferAsync(SELLER, 100, [-chair], Ct);
        var offerId = (await marketplace.SearchOffersAsync(-1, -1, "", 1, true, Ct))
            .Offers[0]
            .OfferId;
        await marketplace.BuyOfferAsync(BUYER, offerId, Ct);

        var listing = _consumeCauses.Should().ContainSingle().Subject;
        (listing.Cause, listing.ActorPlayerId, listing.FirstSeq)
            .Should()
            .Be((MovementCauses.MARKET_LIST, SELLER, (short)0));

        // The item, gone from the inventory, becomes its offer in the same operation.
        var listed = Movements().Should().ContainSingle().Subject;
        (listed.OperationId, listed.Seq, listed.Cause, listed.CauseRef)
            .Should()
            .Be((listing.OperationId, (short)1, MovementCauses.MARKET_LIST, offerId.ToString()));
        (listed.AssetId, listed.DefinitionId, listed.SnapshotExtraData)
            .Should()
            .Be((chair, CHAIR, "state:1"));
        (listed.FromKind, listed.ToKind, listed.ToOwner, listed.ToPlace)
            .Should()
            .Be((PlaceKind.None, PlaceKind.MarketplaceOffer, SELLER, (long)offerId));

        // The sale gives a new row under the buyer's purchase, after its price, naming the offer.
        var sale = _grantCauses.Should().ContainSingle().Subject;
        (sale.Cause, sale.CauseRef, sale.ActorPlayerId, sale.FirstSeq)
            .Should()
            .Be((MovementCauses.MARKET_BUY, offerId.ToString(), BUYER, (short)1));
    }

    [Fact]
    public async Task Taking_an_offer_back_names_the_offer()
    {
        var chair = Give(SELLER, CHAIR);
        await _marketplace.MakeOfferAsync(SELLER, 100, [-chair], Ct);
        var offerId = (await _marketplace.GetOwnOffersAsync(SELLER, 1, Ct)).Offers[0].OfferId;

        (await _marketplace.CancelOfferAsync(SELLER, offerId, Ct)).Should().BeTrue();

        var takeBack = _grantCauses.Should().ContainSingle().Subject;
        (takeBack.Cause, takeBack.CauseRef, takeBack.ActorPlayerId)
            .Should()
            .Be((MovementCauses.MARKET_CANCEL, offerId.ToString(), SELLER));
    }

    private MarketplaceService NewMarketplace(IAssetMovementRecorder movements) =>
        new(
            _catalog.Db,
            Options.Create(
                new CatalogConfig { Marketplace = new MarketplaceConfig { Enabled = true } }
            ),
            _catalog.Fakes.Create<IGrainFactory>(),
            _catalog.Definitions,
            _catalog.Fakes.Create<IInventoryFurnitureLoader>(),
            _time,
            movements,
            NullLogger<MarketplaceService>.Instance
        );

    private async Task<AssetMovementRecorder> RecordingAsync()
    {
        var recorder = new AssetMovementRecorder(
            new TestOptionsMonitor<MovementsConfig>(new() { Enabled = true }),
            _catalog.Db,
            NullLogger<AssetMovementRecorder>.Instance
        );
        await recorder.StartAsync(Ct);

        return recorder;
    }

    private List<AssetMovementOutboxEntity> Movements()
    {
        using var db = _catalog.Db.CreateDbContext();

        return db
            .AssetMovementOutbox.AsNoTracking()
            .Where(x =>
                x.Cause != MovementCauses.RECORDING_STARTED
                && x.Cause != MovementCauses.RECORDING_STOPPED
            )
            .OrderBy(x => x.Id)
            .ToList();
    }

    [Fact]
    public async Task Offered_items_leave_the_inventory_and_list_as_one_row_per_kind()
    {
        var first = Give(SELLER, CHAIR);
        var second = Give(SELLER, CHAIR);
        var lamp = Give(SELLER, LAMP);

        (await _marketplace.MakeOfferAsync(SELLER, 12, [-first], Ct))
            .Should()
            .Be(MarketplaceMakeOfferCode.Success);
        (await _marketplace.MakeOfferAsync(SELLER, 10, [-second], Ct))
            .Should()
            .Be(MarketplaceMakeOfferCode.Success);
        (await _marketplace.MakeOfferAsync(SELLER, 90, [-lamp], Ct))
            .Should()
            .Be(MarketplaceMakeOfferCode.Success);

        _inventories[SELLER].Should().BeEmpty();

        var cheapestFirst = await _marketplace.SearchOffersAsync(-1, -1, "", 2, true, Ct);

        cheapestFirst.TotalItemsFound.Should().Be(2);
        cheapestFirst
            .Offers.Select(x => (x.FurniTypeId, x.Price, x.OfferCount))
            .Should()
            .Equal((CHAIR, 10, 2), (LAMP, 90, 1));

        var dearestFirst = await _marketplace.SearchOffersAsync(-1, -1, "", 1, true, Ct);

        dearestFirst.Offers.Select(x => x.FurniTypeId).Should().Equal(LAMP, CHAIR);

        var byName = await _marketplace.SearchOffersAsync(-1, -1, "lamp", 1, true, Ct);

        byName.Offers.Select(x => x.FurniTypeId).Should().Equal(LAMP);
        (await _marketplace.SearchOffersAsync(13, 50, "", 1, true, Ct))
            .TotalItemsFound.Should()
            .Be(0);

        var open = await _marketplace.GetOwnOffersAsync(SELLER, 1, Ct);

        open.Offers.Should().HaveCount(3);
        open.Offers.Should()
            .OnlyContain(x =>
                x.Status == MarketplaceOfferStatus.Open && x.TimeLeftMinutes == 168 * 60
            );
    }

    [Fact]
    public async Task A_purchase_pays_the_price_gives_the_item_and_the_seller_redeems_the_revenue()
    {
        var chair = Give(SELLER, CHAIR, "state:1");
        await _marketplace.MakeOfferAsync(SELLER, 100, [-chair], Ct);
        var offerId = (await _marketplace.SearchOffersAsync(-1, -1, "", 1, true, Ct))
            .Offers[0]
            .OfferId;

        var bought = await _marketplace.BuyOfferAsync(BUYER, offerId, Ct);

        bought
            .Should()
            .Be(new MarketplaceBuyResult(MarketplaceBuyResultCode.Bought, offerId, 100, offerId));
        _credits[BUYER].Should().Be(900);
        _inventories[BUYER].Values.Should().Equal((CHAIR, "state:1"));
        (await _marketplace.SearchOffersAsync(-1, -1, "", 1, true, Ct))
            .TotalItemsFound.Should()
            .Be(0);

        // 100 * (2 % + 0.5 * 100 / 400000) = 2.0125 -> fee 3 (Flash's calculateFinalPrice).
        var sold = await _marketplace.GetOwnOffersAsync(SELLER, 2, Ct);

        sold.CreditsWaiting.Should().Be(97);
        sold.Offers.Should().ContainSingle().Which.Status.Should().Be(MarketplaceOfferStatus.Sold);

        (await _marketplace.RedeemCreditsAsync(SELLER, Ct)).Should().Be(97);
        (await _marketplace.RedeemCreditsAsync(SELLER, Ct)).Should().Be(0);
        _credits[SELLER].Should().Be(97);
        (await _marketplace.GetOwnOffersAsync(SELLER, 2, Ct)).CreditsWaiting.Should().Be(0);
    }

    [Fact]
    public async Task A_buyer_who_lost_the_race_pays_nothing_and_is_offered_the_next_cheapest()
    {
        await _marketplace.MakeOfferAsync(SELLER, 10, [-Give(SELLER, CHAIR)], Ct);
        await _marketplace.MakeOfferAsync(SELLER, 15, [-Give(SELLER, CHAIR)], Ct);
        var cheapest = (await _marketplace.SearchOffersAsync(-1, -1, "", 2, true, Ct)).Offers[0];

        await _marketplace.BuyOfferAsync(OTHER, cheapest.OfferId, Ct);
        var late = await _marketplace.BuyOfferAsync(BUYER, cheapest.OfferId, Ct);

        late.Code.Should().Be(MarketplaceBuyResultCode.PriceChanged);
        late.NewPrice.Should().Be(15);
        late.RequestedOfferId.Should().Be(cheapest.OfferId);
        _credits[BUYER].Should().Be(1000);
        _inventories[BUYER].Should().BeEmpty();

        (await _marketplace.BuyOfferAsync(BUYER, late.OfferId, Ct))
            .Code.Should()
            .Be(MarketplaceBuyResultCode.Bought);
        (await _marketplace.BuyOfferAsync(OTHER, late.OfferId, Ct))
            .Code.Should()
            .Be(MarketplaceBuyResultCode.SoldOut);
    }

    [Fact]
    public async Task A_buyer_short_of_credits_is_told_so_and_the_offer_stays()
    {
        await _marketplace.MakeOfferAsync(SELLER, 5000, [-Give(SELLER, LAMP)], Ct);
        var offer = (await _marketplace.SearchOffersAsync(-1, -1, "", 1, true, Ct)).Offers[0];

        (await _marketplace.BuyOfferAsync(BUYER, offer.OfferId, Ct))
            .Code.Should()
            .Be(MarketplaceBuyResultCode.NotEnoughCredits);
        _credits[BUYER].Should().Be(1000);
        (await _marketplace.SearchOffersAsync(-1, -1, "", 1, true, Ct))
            .TotalItemsFound.Should()
            .Be(1);
    }

    [Fact]
    public async Task An_offer_taken_back_returns_its_item_and_only_its_seller_can_take_it()
    {
        await _marketplace.MakeOfferAsync(SELLER, 10, [-Give(SELLER, CHAIR, "state:2")], Ct);
        var offerId = (await _marketplace.GetOwnOffersAsync(SELLER, 1, Ct)).Offers[0].OfferId;

        (await _marketplace.CancelOfferAsync(BUYER, offerId, Ct)).Should().BeFalse();
        (await _marketplace.CancelOfferAsync(SELLER, offerId, Ct)).Should().BeTrue();
        (await _marketplace.CancelOfferAsync(SELLER, offerId, Ct)).Should().BeFalse();

        _inventories[SELLER].Values.Should().Equal((CHAIR, "state:2"));
        (await _marketplace.GetOwnOffersAsync(SELLER, 1, Ct)).Offers.Should().BeEmpty();
    }

    [Fact]
    public async Task An_offer_past_its_hours_is_expired_unlisted_and_can_still_be_taken_back()
    {
        await _marketplace.MakeOfferAsync(SELLER, 10, [-Give(SELLER, CHAIR)], Ct);
        _time.Advance(TimeSpan.FromHours(169));

        (await _marketplace.SearchOffersAsync(-1, -1, "", 1, true, Ct))
            .TotalItemsFound.Should()
            .Be(0);
        (await _marketplace.GetOwnOffersAsync(SELLER, 1, Ct)).Offers.Should().BeEmpty();

        var expired = (await _marketplace.GetOwnOffersAsync(SELLER, 3, Ct))
            .Offers.Should()
            .ContainSingle()
            .Subject;

        expired.Status.Should().Be(MarketplaceOfferStatus.Expired);
        (await _marketplace.BuyOfferAsync(BUYER, expired.OfferId, Ct))
            .Code.Should()
            .Be(MarketplaceBuyResultCode.SoldOut);
        (await _marketplace.CancelOfferAsync(SELLER, expired.OfferId, Ct)).Should().BeTrue();
        _inventories[SELLER].Should().HaveCount(1);
    }

    [Fact]
    public async Task Offers_out_of_bounds_or_of_mixed_kinds_are_refused_and_keep_their_items()
    {
        var chair = Give(SELLER, CHAIR);
        var lamp = Give(SELLER, LAMP);

        (await _marketplace.MakeOfferAsync(SELLER, 1, [-chair], Ct))
            .Should()
            .Be(MarketplaceMakeOfferCode.TechnicalError);
        (await _marketplace.MakeOfferAsync(SELLER, 100000, [-chair], Ct))
            .Should()
            .Be(MarketplaceMakeOfferCode.TechnicalError);
        (await _marketplace.MakeOfferAsync(SELLER, 10, [-chair, -lamp], Ct))
            .Should()
            .Be(MarketplaceMakeOfferCode.TechnicalError);
        (await _marketplace.MakeOfferAsync(SELLER, 10, [-12345], Ct))
            .Should()
            .Be(MarketplaceMakeOfferCode.TechnicalError);
        (
            await _marketplace.MakeOfferAsync(
                SELLER,
                10,
                [.. Enumerable.Range(1, 251).Select(x => -x)],
                Ct
            )
        )
            .Should()
            .Be(MarketplaceMakeOfferCode.TooManyItems);

        _inventories[SELLER].Should().HaveCount(2);
        (await _marketplace.CanMakeOfferAsync(SELLER, Ct))
            .Should()
            .Be(new MarketplaceCanMakeOffer(MarketplaceCanMakeOfferCode.Allowed, 0));
    }

    [Fact]
    public void Only_tradeable_furni_that_is_not_credit_furni_is_listed_as_sellable()
    {
        // Official client capture, every inventory recorded (72 kinds): Hutch.'s CF_1_coin_bronze
        // tradeable but not sellable, an untradeable item (bed_silo_two_notrd) neither, a
        // gift_joystick both.
        _catalog.AddDefinition(COIN, "CF_1_coin_bronze");
        _catalog.AddDefinition(BED, "bed_silo_two_notrd", set: x => x.CanTrade = false);

        var packet = PacketHarness.Encode(
            new FurniListAddOrUpdateEventMessageComposer
            {
                Items = [Snapshot(1, (CHAIR, "")), Snapshot(2, (COIN, "")), Snapshot(3, (BED, ""))],
            }
        );
        var sellable = new List<bool>();

        packet.PopInt().Should().Be(3);

        for (var i = 0; i < 3; i++)
        {
            // FurniData: id, type, ref, type id, category, stuff data, then recyclable,
            // tradeable, groupable and sellable (FurniData.as:55-64 @1745).
            packet.PopInt();
            packet.PopString();
            packet.PopInt();
            packet.PopInt();
            packet.PopInt();
            packet.PopInt();
            packet.PopString();
            packet.PopBoolean();
            packet.PopBoolean();
            packet.PopBoolean();
            sellable.Add(packet.PopBoolean());
            packet.PopInt();
            packet.PopBoolean();
            packet.PopInt();
            packet.PopString();
            packet.PopInt();
        }

        sellable.Should().Equal(true, false, false);
    }

    [Fact]
    public async Task Credit_furni_and_untradeable_furni_are_refused_and_keep_their_items()
    {
        _catalog.AddDefinition(COIN, "CF_1_coin_bronze");
        _catalog.AddDefinition(BED, "bed_silo_two_notrd", set: x => x.CanTrade = false);
        var coin = Give(SELLER, COIN);
        var bed = Give(SELLER, BED);
        var chair = Give(SELLER, CHAIR);

        (await _marketplace.MakeOfferAsync(SELLER, 10, [-coin], Ct))
            .Should()
            .Be(MarketplaceMakeOfferCode.TechnicalError);
        (await _marketplace.MakeOfferAsync(SELLER, 10, [-bed], Ct))
            .Should()
            .Be(MarketplaceMakeOfferCode.TechnicalError);
        (await _marketplace.MakeOfferAsync(SELLER, 10, [-chair], Ct))
            .Should()
            .Be(MarketplaceMakeOfferCode.Success);

        _inventories[SELLER].Keys.Should().BeEquivalentTo([coin, bed]);
    }

    [Fact]
    public async Task Item_stats_give_the_daily_history_before_today_the_average_and_the_lowest_price()
    {
        foreach (var (price, daysAgo) in new[] { (10, 3), (20, 3), (30, 1), (40, 0) })
        {
            var id = Give(SELLER, CHAIR);

            await _marketplace.MakeOfferAsync(SELLER, price, [-id], Ct);
            var offerId = (await _marketplace.GetOwnOffersAsync(SELLER, 1, Ct)).Offers.Max(x =>
                x.OfferId
            );

            _time.Set(NOW.AddDays(-daysAgo));
            await _marketplace.BuyOfferAsync(BUYER, offerId, Ct);
            _time.Set(NOW);
        }

        await _marketplace.MakeOfferAsync(SELLER, 25, [-Give(SELLER, CHAIR)], Ct);
        await _marketplace.MakeOfferAsync(SELLER, 22, [-Give(SELLER, CHAIR)], Ct);

        var stats = await _marketplace.GetItemStatsAsync(1, CHAIR, null, Ct);

        stats.HistoryLength.Should().Be(30);
        stats
            .Days.Select(x => (x.DayOffset, x.AveragePrice, x.SoldAmount))
            .Should()
            .Equal((-3, 15, 2), (-1, 30, 1));
        stats.AveragePrice.Should().Be(25);
        stats.OfferCount.Should().Be(2);
        stats.LowestCurrentPrice.Should().Be(22);
        // Official: the lowest open price is suggested over the average (LTD 17676: 99 against 568).
        stats.SuggestedPrice.Should().Be(22);
        stats.FurniCategoryId.Should().Be(1);
        stats.FurniTypeId.Should().Be(CHAIR);
    }

    [Fact]
    public void Offers_are_written_as_the_official_client_capture_shows_them()
    {
        // Three rows of an official client capture (2026-10-13 17:48, MarketPlaceOffers): a floor
        // item, a poster and a limited edition, then the total found.
        var packet = PacketHarness.Encode(
            new MarketPlaceOffersEventMessageComposer
            {
                Offers =
                [
                    Row(186577118, 4405, MarketplaceFurniType.Floor, 99999, 8347, 0, 1) with
                    {
                        StuffData = new LegacyStuffSnapshot { StuffBitmask = 0, Data = "1" },
                    },
                    Row(186236075, 4001, MarketplaceFurniType.Wall, 60000, 1448, 0, 1) with
                    {
                        ExtraData = "2005",
                    },
                    Row(186373106, 17676, MarketplaceFurniType.Unique, 99, 5185, 549, 21) with
                    {
                        StuffData = new LegacyStuffSnapshot
                        {
                            StuffBitmask = 256,
                            Data = "",
                            UniqueNumber = 1893,
                            UniqueSeries = 1920,
                        },
                    },
                ],
                TotalItemsFound = 10972,
            }
        );

        Convert
            .ToHexString(packet.PopBytes(packet.Remaining))
            .Should()
            .Be(
                "00000003"
                    + "0B1EF0DE000000010000000100001135000000000001310001869F0000209B0000000000000001"
                    + "0B19BCAB000000010000000200000FA10004323030350000EA60000005A80000000000000001"
                    + "0B1BD3F200000001000000030000450C0000076500000780000000630000144100000225"
                    + "00000015"
                    + "00002ADC"
            );
    }

    [Fact]
    public void Own_offers_carry_the_credits_waiting_and_a_sold_offer_its_time()
    {
        var packet = PacketHarness.Encode(
            new MarketPlaceOwnOffersEventMessageComposer
            {
                CreditsWaiting = 97,
                Offers =
                [
                    Row(5, CHAIR, MarketplaceFurniType.Wall, 100, 0, 0, 0) with
                    {
                        Status = MarketplaceOfferStatus.Sold,
                        ExtraData = "",
                        StatusTime = 1,
                    },
                ],
            }
        );

        Convert
            .ToHexString(packet.PopBytes(packet.Remaining))
            .Should()
            .Be(
                "0000006100000001"
                    + "00000005"
                    + "00000002"
                    + "00000002"
                    + "0000000A"
                    + "0000"
                    + "00000064"
                    + "00000000"
                    + "00000000"
                    + "0000000000000001"
            );
    }

    [Fact]
    public async Task Taking_back_all_offers_returns_every_unsold_item_and_names_them()
    {
        await _marketplace.MakeOfferAsync(SELLER, 10, [-Give(SELLER, CHAIR)], Ct);
        await _marketplace.MakeOfferAsync(SELLER, 20, [-Give(SELLER, LAMP)], Ct);
        await _marketplace.MakeOfferAsync(SELLER, 30, [-Give(SELLER, LAMP)], Ct);
        var sold = (await _marketplace.SearchOffersAsync(-1, -1, "", 2, true, Ct)).Offers[0];
        await _marketplace.BuyOfferAsync(BUYER, sold.OfferId, Ct);
        _time.Advance(TimeSpan.FromHours(100));
        await _marketplace.MakeOfferAsync(SELLER, 40, [-Give(SELLER, CHAIR)], Ct);
        _time.Advance(TimeSpan.FromHours(100));

        var taken = await _marketplace.CancelAllOffersAsync(SELLER, Ct);

        taken.Should().HaveCount(3).And.NotContain(sold.OfferId);
        _inventories[SELLER]
            .Values.Select(x => x.Definition)
            .Should()
            .BeEquivalentTo([LAMP, LAMP, CHAIR]);
        (await _marketplace.GetOwnOffersAsync(SELLER, 1, Ct)).Offers.Should().BeEmpty();
        (await _marketplace.GetOwnOffersAsync(SELLER, 3, Ct)).Offers.Should().BeEmpty();
        (await _marketplace.GetOwnOffersAsync(SELLER, 2, Ct)).Offers.Should().ContainSingle();
    }

    [Fact]
    public async Task Clearing_history_hides_sold_offers_keeps_their_credits_and_returns_expired_items()
    {
        await _marketplace.MakeOfferAsync(SELLER, 100, [-Give(SELLER, CHAIR)], Ct);
        var offerId = (await _marketplace.GetOwnOffersAsync(SELLER, 1, Ct)).Offers[0].OfferId;
        await _marketplace.BuyOfferAsync(BUYER, offerId, Ct);
        await _marketplace.MakeOfferAsync(SELLER, 50, [-Give(SELLER, LAMP)], Ct);
        _time.Advance(TimeSpan.FromHours(169));

        (await _marketplace.ClearOwnHistoryAsync(SELLER, 1, Ct)).Should().BeFalse();
        (await _marketplace.ClearOwnHistoryAsync(SELLER, 2, Ct)).Should().BeTrue();
        (await _marketplace.ClearOwnHistoryAsync(SELLER, 3, Ct)).Should().BeTrue();

        var soldList = await _marketplace.GetOwnOffersAsync(SELLER, 2, Ct);

        soldList.Offers.Should().BeEmpty();
        soldList.CreditsWaiting.Should().Be(97);
        (await _marketplace.GetOwnOffersAsync(SELLER, 3, Ct)).Offers.Should().BeEmpty();
        _inventories[SELLER].Values.Select(x => x.Definition).Should().Equal(LAMP);
        (await _marketplace.GetItemStatsAsync(1, CHAIR, null, Ct)).Days.Should().ContainSingle();
    }

    [Fact]
    public void Cancel_all_and_clear_history_results_are_written_as_Flash_reads_them()
    {
        var all = PacketHarness.Encode(
            new MarketplaceCancelAllOffersResultMessageComposer
            {
                OfferIds = [5, 6],
                Success = true,
            }
        );
        var clear = PacketHarness.Encode(
            new MarketplaceClearOwnHistoryResultMessageComposer { Success = true }
        );

        Convert.ToHexString(all.PopBytes(all.Remaining)).Should().Be("00000002000000050000000601");
        Convert.ToHexString(clear.PopBytes(clear.Remaining)).Should().Be("01");
    }

    [Fact]
    public async Task A_sale_waits_in_the_earnings_vault_and_its_claim_pays_the_seller()
    {
        var vault = new IncomeRewardService(
            [new MarketplaceIncomeRewardSource(_marketplace)],
            _catalog.Db,
            _catalog.Fakes.Create<IGrainFactory>(),
            _time,
            Options.Create(new CatalogConfig()),
            NullLogger<IncomeRewardService>.Instance
        );
        var first = Give(SELLER, CHAIR);
        var second = Give(SELLER, LAMP);
        await _marketplace.MakeOfferAsync(SELLER, 100, [-first], Ct);
        await _marketplace.MakeOfferAsync(SELLER, 50, [-second], Ct);

        foreach (
            var offer in (await _marketplace.SearchOffersAsync(-1, -1, "", 1, true, Ct)).Offers
        )
            await _marketplace.BuyOfferAsync(BUYER, offer.OfferId, Ct);

        // The online seller hears of each sale as Flash's "notification.earning.new" for category 3.
        _catalog
            .Fakes.Log.Of("TrySendComposerAsync")
            .Select(x => x.Args[0])
            .Should()
            .Equal(
                Enumerable.Repeat(
                    new IncomeRewardNotificationMessageComposer
                    {
                        RewardCategory = VaultRewardCategoryType.Marketplace,
                    },
                    2
                )
            );

        // 100 -> 97 and 50 -> 48 after the fee (calculateFinalPrice), one credits row per sale.
        (await vault.GetRewardsAsync(SELLER, Ct))
            .Select(x => (x.RewardCategory, x.RewardType, x.Amount, x.ProductCode))
            .Should()
            .BeEquivalentTo([
                (VaultRewardCategoryType.Marketplace, VaultRewardType.Credits, 97, ""),
                (VaultRewardCategoryType.Marketplace, VaultRewardType.Credits, 48, ""),
            ]);
        (await vault.GetRewardsAsync(BUYER, Ct)).Should().BeEmpty();

        (await vault.ClaimAsync(SELLER, VaultRewardCategoryType.Achievements, Ct))
            .Should()
            .BeFalse();
        _credits[SELLER].Should().Be(0);

        (await vault.ClaimAsync(SELLER, VaultRewardCategoryType.Marketplace, Ct)).Should().BeTrue();
        _credits[SELLER].Should().Be(145);
        (await vault.GetRewardsAsync(SELLER, Ct)).Should().BeEmpty();
        (await _marketplace.GetOwnOffersAsync(SELLER, 2, Ct)).CreditsWaiting.Should().Be(0);

        (await vault.ClaimAsync(SELLER, VaultRewardCategoryType.All, Ct)).Should().BeTrue();
        _credits[SELLER].Should().Be(145);
    }

    [Fact]
    public void Earnings_packets_are_written_and_read_as_Flash_does()
    {
        // IncomeRewardClaimMessageComposer: one signed byte, -1 for Claim All.
        PacketHarness
            .Parse(
                PacketHarness.Incoming("IncomeRewardClaimMessageEvent"),
                PacketHarness.Payload(w => w.Byte(0xFF))
            )
            .Should()
            .Be(new IncomeRewardClaimMessage { RewardCategory = VaultRewardCategoryType.All });
        PacketHarness
            .Parse(
                PacketHarness.Incoming("IncomeRewardClaimMessageEvent"),
                PacketHarness.Payload(w => w.Byte(3))
            )
            .Should()
            .Be(
                new IncomeRewardClaimMessage
                {
                    RewardCategory = VaultRewardCategoryType.Marketplace,
                }
            );

        var status = PacketHarness.Encode(
            new IncomeRewardStatusMessageComposer
            {
                IncomeRewards =
                [
                    new IncomeRewardSnapshot
                    {
                        RewardCategory = VaultRewardCategoryType.Marketplace,
                        RewardType = VaultRewardType.Credits,
                        Amount = 97,
                        ProductCode = "",
                    },
                ],
            }
        );
        var claimAll = PacketHarness.Encode(
            new IncomeRewardClaimResponseMessageComposer
            {
                RewardCategory = VaultRewardCategoryType.All,
                Result = true,
            }
        );
        var notification = PacketHarness.Encode(
            new IncomeRewardNotificationMessageComposer
            {
                RewardCategory = VaultRewardCategoryType.Marketplace,
            }
        );

        // EarningsView adds rewardType 1 into CreditValue (EarningsView.as:118).
        Convert
            .ToHexString(status.PopBytes(status.Remaining))
            .Should()
            .Be("00000001" + "03" + "01" + "00000061" + "0000");
        Convert.ToHexString(claimAll.PopBytes(claimAll.Remaining)).Should().Be("FF01");
        Convert.ToHexString(notification.PopBytes(notification.Remaining)).Should().Be("03");
    }

    private static MarketplaceOfferSnapshot Row(
        int offerId,
        int furniTypeId,
        MarketplaceFurniType type,
        int price,
        int minutesLeft,
        int average,
        int count
    ) =>
        new()
        {
            OfferId = offerId,
            FurniTypeId = furniTypeId,
            FurniType = type,
            ExtraData = "",
            StuffData = null,
            Price = price,
            Status = MarketplaceOfferStatus.Open,
            TimeLeftMinutes = minutesLeft,
            AveragePrice = average,
            OfferCount = count,
        };

    private int Give(int player, int definition, string extra = "")
    {
        var id = _nextItemId++;

        _inventories[player][id] = (definition, extra);

        return id;
    }

    /// <summary>An item as the inventory lists it; "ltd:n/s" in its extra data makes it a limited edition.</summary>
    private FurnitureItemSnapshot Snapshot(int id, (int Definition, string Extra) item)
    {
        var definition = _catalog.Definitions.TryGetDefinition(item.Definition)!;
        var ltd = item.Extra.StartsWith("ltd:", StringComparison.Ordinal)
            ? item.Extra[4..].Split('/').Select(int.Parse).ToArray()
            : null;

        return new FurnitureItemSnapshot
        {
            ItemId = id,
            SpriteId = definition.SpriteId,
            OwnerId = 0,
            OwnerName = "",
            Definition = definition,
            StuffData = new LegacyStuffSnapshot
            {
                StuffBitmask = ltd is null ? 0 : 256,
                Data = item.Extra,
                UniqueNumber = ltd?[0] ?? 0,
                UniqueSeries = ltd?[1] ?? 0,
            },
            ExtraData = item.Extra,
            SecondsToExpiration = -1,
            HasRentPeriodStarted = false,
            RoomId = 0,
        };
    }
}
