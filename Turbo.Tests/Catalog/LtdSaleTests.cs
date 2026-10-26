using System.Collections.Concurrent;
using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Orleans.Runtime;
using Orleans.Timers;
using Turbo.Catalog;
using Turbo.Catalog.Configuration;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Events;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Messages.Outgoing.Collectibles;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// A limited edition sold as the hotel sells one: its raffle gathers buyers, the draw answers
/// each of them (<c>HabboCatalog.onLtdRaffleResult</c> ends the purchase window's "processing"),
/// every item carries its own serial, the catalog's "left" count follows the sales, and a buyer
/// who comes too late gets the sold-out alert (<c>HabboCatalog.onLimitedEditionSoldOut</c>).
/// </summary>
public sealed class LtdSaleTests : IDisposable
{
    private const int SERIES = 9;
    private const int SIZE = 5;

    private readonly CatalogFixture _catalog = new();
    private readonly TestEventBus _bus = new();
    private readonly ConcurrentQueue<(object? Player, IComposer Composer)> _sent = new();
    private readonly ConcurrentQueue<int> _serials = new();
    private Func<object?, CancellationToken, Task>? _timer;
    private CancellationTokenSource? _timerToken;
    private object? _timerState;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _catalog.Dispose();

    [Fact]
    public async Task TheRaffleDrawnByItsTimer_AnswersEveryEntrant()
    {
        var (grain, _) = await SeriesAsync(raffled: false);

        (await grain.EnterRaffleAsync(7, Ct)).Success.Should().BeTrue();
        (await grain.EnterRaffleAsync(8, Ct)).Success.Should().BeTrue();

        // Orleans cancels a grain timer's token when the timer is disposed, as the draw does.
        await _timer!(_timerState, _timerToken!.Token);

        Results()
            .Should()
            .BeEquivalentTo([(7, LtdRaffleResultType.Won), (8, LtdRaffleResultType.Won)]);
        _serials.Should().OnlyHaveUniqueItems().And.HaveCount(2);
        Remaining().Should().Be(SIZE - 2);
    }

    [Fact]
    public async Task ASeriesRemovedWhileItsRaffleGathers_TakesNobodyElse_AndItsBatchAllLose()
    {
        var (grain, _) = await SeriesAsync(raffled: false);

        (await grain.EnterRaffleAsync(7, Ct)).Success.Should().BeTrue();
        (await grain.EnterRaffleAsync(8, Ct)).Success.Should().BeTrue();

        // Taken away from the editor, which then tells the raffle.
        await using (var db = await _catalog.Db.CreateDbContextAsync(Ct))
        {
            await db.LtdRaffleEntries.ExecuteDeleteAsync(Ct);
            await db.LtdSeries.ExecuteDeleteAsync(Ct);
        }

        await grain.ReloadSeriesAsync(Ct);

        (await grain.EnterRaffleAsync(9, Ct))
            .Error.Should()
            .Be(LtdRaffleEntryErrorType.SeriesNotFound);

        await _timer!(_timerState, _timerToken!.Token);

        // Everyone in the batch is answered, nobody is charged, and nothing is handed out.
        Results()
            .Should()
            .BeEquivalentTo([(7, LtdRaffleResultType.Lost), (8, LtdRaffleResultType.Lost)]);
        _serials.Should().BeEmpty();
        _catalog.Fakes.Log.Of("TryDebitAsync").Should().BeEmpty();
    }

    // HC Payday counted no limited edition a member bought (user report 2026-10-26): the sale
    // never said a catalogue purchase had completed. The club centre says HC members get credits
    // back from "catalog purchases" (hccenter.special.info); that an LTD is one is inference.
    [Fact]
    [Trait("evidence", "inferred")]
    public async Task EverySale_IsACompletedCataloguePurchaseAtTheOffersPrice()
    {
        var (grain, _) = await SeriesAsync(raffled: false);

        _bus.Record<PurchaseCompletedEvent>();

        await grain.EnterRaffleAsync(7, Ct);
        await grain.EnterRaffleAsync(8, Ct);
        await _timer!(_timerState, _timerToken!.Token);
        await _bus.WhenPublishedAsync<PurchaseCompletedEvent>(2);

        _bus.Of<PurchaseCompletedEvent>()
            .Select(x => (x.PlayerId.Value, x.OfferId, x.CostCredits, x.Units))
            .Should()
            .BeEquivalentTo([(7, SOLD, 5, 1), (8, SOLD, 5, 1)]);
    }

    [Fact]
    public async Task ARunningBatch_KeepsTheGrainLoadedPastItsWindow()
    {
        var (grain, _) = await SeriesAsync(raffled: false);

        (await grain.EnterRaffleAsync(7, Ct)).Success.Should().BeTrue();
        (await grain.EnterRaffleAsync(8, Ct)).Success.Should().BeTrue();

        // Deactivation draws the batch, so idle collection before the timer would end the
        // raffle early: the series' 30 s window plus the margin for the draw to run.
        Pins().Should().Equal(TimeSpan.FromSeconds(30) + TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task SalesAfterTheRaffle_NeverRepeatASerial()
    {
        var (grain, _) = await SeriesAsync(raffled: true);

        for (var player = 7; player < 7 + SIZE; player++)
            (await grain.EnterRaffleAsync(player, Ct)).Success.Should().BeTrue();

        _serials.Should().BeEquivalentTo(Enumerable.Range(1, SIZE));
        Remaining().Should().Be(0);

        var late = await grain.EnterRaffleAsync(20, Ct);

        late.Error.Should().Be(LtdRaffleEntryErrorType.SoldOut);
    }

    [Fact]
    public async Task EverySale_LowersTheLeftCountTheCatalogSends()
    {
        var (grain, catalog) = await SeriesAsync(raffled: true);

        await grain.EnterRaffleAsync(7, Ct);
        await grain.EnterRaffleAsync(8, Ct);

        catalog.Current.ProductsById[SOLD].UniqueRemaining.Should().Be(SIZE - 2);
        catalog.Current.OffersById[SOLD].Products.Single().UniqueRemaining.Should().Be(SIZE - 2);
    }

    [Fact]
    public async Task ASoldOutLimitedEdition_SaysSoInsteadOfAPurchaseError()
    {
        var (_, catalog) = await SeriesAsync(raffled: true);
        var harness = new PacketHarness();

        harness.Fakes.Handlers["GetCatalogSnapshot"] = _ => catalog.Current;
        harness.Fakes.Handlers["EnterRaffleAsync"] = _ =>
            Task.FromResult(LtdRaffleEntryResult.Failed(LtdRaffleEntryErrorType.SoldOut));
        harness.Resolver.Overrides[typeof(ICatalogService)] =
            harness.Fakes.Create<ICatalogService>();

        var replies = await harness.SendAsync(
            PacketHarness.Incoming("PurchaseFromCatalogMessageEvent"),
            PacketHarness.Payload(w => w.Int(FURNITURE).Int(SOLD).String("").Int(1)),
            playerId: 7
        );

        replies
            .Select(x => x.Header)
            .Should()
            .Equal(PacketHarness.Outgoing("LimitedEditionSoldOutMessageComposer"));
    }

    /// <summary>
    /// The offer <see cref="SOLD"/> made a limited series, and the grain that sells it: with its
    /// opening raffle to come, or drawn already so each purchase is a sale of its own.
    /// </summary>
    private async Task<(
        ICatalogLtdRaffleGrain Grain,
        Turbo.Catalog.Providers.CatalogSnapshotProvider<Turbo.Primitives.Catalog.Tags.NormalCatalog> Catalog
    )> SeriesAsync(bool raffled)
    {
        _catalog.Db.Insert(
            new LtdSeriesEntity
            {
                Id = SERIES,
                CatalogProductEntityId = SOLD,
                TotalQuantity = SIZE,
                RemainingQuantity = SIZE,
                RaffleWindowSeconds = 30,
                IsActive = true,
                IsRaffleFinished = raffled,
            }
        );

        foreach (var player in Enumerable.Range(7, SIZE).Append(20))
        {
            _catalog.Db.Insert(
                new PlayerEntity
                {
                    Id = player,
                    Name = $"buyer{player}",
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                    PlayerStatus = PlayerStatusType.Offline,
                }
            );
        }

        var catalog = _catalog.NormalProvider();

        await catalog.ReloadAsync(Ct);

        var fakes = _catalog.Fakes;

        fakes.Handlers["GetCatalogSnapshot"] = _ => catalog.Current;
        fakes.Handlers["SetLtdRemaining"] = call =>
        {
            catalog.SetLtdRemaining((int)call.Args[0]!, (int)call.Args[1]!);

            return null;
        };
        fakes.Handlers["GetAmountForCurrencyAsync"] = _ => Task.FromResult(1000);
        fakes.Handlers["GetActivityPointsAsync"] = _ => Task.FromResult(new Dictionary<int, int>());
        fakes.Handlers["TryDebitAsync"] = _ => Task.FromResult(WalletDebitResult.Success());
        fakes.Handlers["GrantLtdFurnitureAsync"] = call =>
        {
            _serials.Enqueue((int)call.Args[1]!);

            return Task.CompletedTask;
        };
        fakes.Handlers["SendComposerAsync"] = call =>
        {
            _sent.Enqueue((call.Key, (IComposer)call.Args[0]!));

            return Task.CompletedTask;
        };
        fakes.Handlers["RegisterGrainTimer"] = call =>
        {
            _timer = (Func<object?, CancellationToken, Task>)call.Args[1]!;
            _timerState = call.Args[2];
            _timerToken = new CancellationTokenSource();

            return fakes.Create<IGrainTimer>("raffle");
        };
        fakes.Handlers["Dispose"] = call =>
        {
            if (call.Interface == typeof(IGrainTimer))
                _timerToken?.Cancel();

            return null;
        };

        var grain = GrainHarness.Create(
            typeof(CatalogModule).Assembly,
            "Turbo.Catalog.Grains.CatalogLtdRaffleGrain",
            fakes,
            _catalog.Db
        );

        RoomHarness.SetMember(
            grain
                .GetType()
                .GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(grain)!,
            "SeriesId",
            SERIES
        );
        Set(grain, "_catalogService", fakes.Create<ICatalogService>());
        Set(grain, "_events", _bus.System);
        Set(grain, "_config", Unweighted());
        await ((Grain)grain).OnActivateAsync(Ct);

        return ((ICatalogLtdRaffleGrain)grain, catalog);
    }

    /// <summary>Every buyer weighs the same: the weighting reads grains this test does not run.</summary>
    private static CatalogConfig Unweighted()
    {
        var config = new CatalogConfig();
        var raffle = config.LtdRaffle;

        foreach (
            var criterion in new[]
            {
                raffle.BadgeCount,
                raffle.AccountAgeDays,
                raffle.RoomCount,
                raffle.FurnitureCount,
                raffle.FriendCount,
                raffle.RespectsReceived,
                raffle.AchievementScore,
            }
        )
            criterion.Enabled = false;

        return config;
    }

    private List<(int Player, LtdRaffleResultType Result)> Results() =>
        [
            .. _sent
                .Where(x => x.Composer is LtdRaffleResultMessageComposer)
                .Select(x =>
                    (
                        Convert.ToInt32(x.Player!.ToString()),
                        ((LtdRaffleResultMessageComposer)x.Composer).ResultCode
                    )
                ),
        ];

    private List<TimeSpan> Pins() =>
        [.. _catalog.Fakes.Log.Of("DelayDeactivation").Select(call => (TimeSpan)call.Args[1]!)];

    private int Remaining()
    {
        using var db = _catalog.Db.CreateDbContext();

        return db.LtdSeries.Single(x => x.Id == SERIES).RemainingQuantity;
    }

    private static void Set(object grain, string field, object value) =>
        grain
            .GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(grain, value);
}
