using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Turbo.Catalog;
using Turbo.Catalog.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Players;
using Turbo.Main;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Messages.Outgoing.Collectibles;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// A limited series sold by raffle, as Habbo sells one: buyers press buy as usual during a
/// window that opens with the sale (20 s on Habbo), the draw favours experienced players, the
/// selected are charged and given their piece, and what is left is sold first come, first
/// served. The draw disposed its own timer first, which cancels the token the timer hands it:
/// on a real server, 50 entrants and a 30 s window left 0 winners, 50 entries pending and the
/// whole stock unsold.
/// </summary>
public sealed class LtdRaffleTests : IDisposable
{
    private const int SERIES = 9;
    private static readonly int[] Entrants = [7, 8, 9, 10, 11];

    private readonly CatalogFixture _catalog = new();
    private object? _grain;
    private RaffleTimer? _timer;
    private CatalogSnapshot? _snapshot;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public LtdRaffleTests()
    {
        foreach (var id in Entrants)
            _catalog.Db.Insert(
                new PlayerEntity
                {
                    Id = id,
                    Name = $"entrant-{id}",
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                }
            );
    }

    public void Dispose() => _catalog.Dispose();

    [Fact]
    public async Task When_its_window_ends_the_raffle_draws_its_winners()
    {
        var grain = await SeriesGrainAsync(stock: 2, windowSeconds: 20);

        foreach (var id in Entrants[..3])
            (await grain.EnterRaffleAsync(id, Ct)).Success.Should().BeTrue();

        await _timer!.FireAsync();

        var entries = await EntriesAsync();
        entries.Count(x => x.Result == LtdEntryResult.Won).Should().Be(2);
        entries.Count(x => x.Result == LtdEntryResult.Lost).Should().Be(1);
        entries
            .Where(x => x.Result == LtdEntryResult.Won)
            .Select(x => x.SerialNumber)
            .Should()
            .BeEquivalentTo([1, 2]);
        (await StockAsync()).Should().Be(0);
        _catalog.Fakes.Log.Of(nameof(IInventoryGrain.GrantLtdFurnitureAsync)).Should().HaveCount(2);
    }

    /// <summary>
    /// The draw picked as many winners as there was stock; one who could not pay then was told
    /// they lost, their entry stayed pending for good, and their piece went unsold while other
    /// entrants were left. Here the three most experienced cannot pay.
    /// </summary>
    [Fact]
    public async Task A_winner_who_cannot_pay_is_passed_over_and_everyone_hears_how_it_went()
    {
        int[] cannotPay = [7, 8, 9];
        var grain = await SeriesGrainAsync(stock: 2, windowSeconds: 20);
        WeighByAccountAge(experienced: cannotPay);
        _catalog.Fakes.Handlers["TryDebitAsync"] = call =>
            Task.FromResult(
                cannotPay.Contains((int)Convert.ToInt64(call.Key))
                    ? WalletDebitResult.InsufficientBalance(
                        new WalletDebitFailure { CurrencyKind = CurrencyKind.Credits, Amount = 5 }
                    )
                    : WalletDebitResult.Success()
            );

        foreach (var id in Entrants)
            await grain.EnterRaffleAsync(id, Ct);
        await _timer!.FireAsync();

        (await EntriesAsync())
            .Select(x => (x.PlayerEntityId, x.Result))
            .Should()
            .BeEquivalentTo([
                (7, LtdEntryResult.Lost),
                (8, LtdEntryResult.Lost),
                (9, LtdEntryResult.Lost),
                (10, LtdEntryResult.Won),
                (11, LtdEntryResult.Won),
            ]);
        Results()
            .Should()
            .BeEquivalentTo(
                [(7, false), (8, false), (9, false), (10, true), (11, true)],
                "each entrant is told once how it went"
            );
    }

    /// <summary>The panel's 0 means first come, first served; it raffled for 20 s anyway.</summary>
    [Fact]
    public async Task A_window_of_zero_sells_first_come_first_served()
    {
        var grain = await SeriesGrainAsync(stock: 2, windowSeconds: 0);

        (await grain.EnterRaffleAsync(7, Ct)).BatchId.Should().Be("instant");

        _timer.Should().BeNull();
        (await EntriesAsync())
            .Should()
            .ContainSingle()
            .Which.Result.Should()
            .Be(LtdEntryResult.Won);
    }

    /// <summary>
    /// The window opens with the sale, so it closes on time whoever buys first; without a start
    /// time the first buyer opens it.
    /// </summary>
    [Fact]
    public async Task A_series_with_a_start_time_closes_its_window_on_time()
    {
        var grain = await SeriesGrainAsync(
            stock: 2,
            windowSeconds: 20,
            startsAt: DateTime.UtcNow.AddSeconds(-5)
        );

        await grain.EnterRaffleAsync(7, Ct);

        _timer!.DueTime.Should().BeCloseTo(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// A raffle still gathering buyers when the server stopped was drawn after the silo had
    /// left the cluster: marked finished, every charge failed, every entry left pending.
    /// </summary>
    [Fact]
    public async Task An_open_raffle_is_drawn_before_the_server_stops()
    {
        var grain = await SeriesGrainAsync(stock: 2, windowSeconds: 20);
        foreach (var id in Entrants[..2])
            await grain.EnterRaffleAsync(id, Ct);
        _catalog.Fakes.Instances[(typeof(ICatalogLtdRaffleGrain), (object)(long)SERIES)] = grain;

        await Emulator().StopAsync(Ct);

        (await EntriesAsync()).Should().OnlyContain(x => x.Result == LtdEntryResult.Won);
        (await StockAsync()).Should().Be(0);
    }

    /// <summary>A sold-out series was a purchase error for an offer not found.</summary>
    [Fact]
    public async Task A_sold_out_series_is_the_clients_own_sold_out_alert()
    {
        await SeriesGrainAsync(stock: 2, windowSeconds: 0);
        var harness = new PacketHarness();
        harness.Resolver.Overrides[typeof(ICatalogService)] =
            harness.Fakes.Create<ICatalogService>();
        harness.Fakes.Handlers["GetCatalogSnapshot"] = _ => _snapshot;
        harness.Fakes.Handlers[nameof(ICatalogLtdRaffleGrain.EnterRaffleAsync)] = _ =>
            Task.FromResult(LtdRaffleEntryResult.Failed(LtdRaffleEntryErrorType.SoldOut));

        var replies = await harness.SendAsync(
            PacketHarness.Incoming("PurchaseFromCatalogMessageEvent"),
            PacketHarness.Payload(w => w.Int(FURNITURE).Int(SOLD).String("").Int(1)),
            playerId: 7
        );

        replies
            .Should()
            .ContainSingle()
            .Which.Header.Should()
            .Be(PacketHarness.Outgoing("LimitedEditionSoldOutMessageComposer"));
    }

    private async Task<ICatalogLtdRaffleGrain> SeriesGrainAsync(
        int stock,
        int windowSeconds,
        DateTime? startsAt = null
    )
    {
        _catalog.Db.Insert(
            new LtdSeriesEntity
            {
                Id = SERIES,
                CatalogProductEntityId = SOLD,
                TotalQuantity = stock,
                RemainingQuantity = stock,
                RaffleWindowSeconds = windowSeconds,
                IsActive = true,
                IsRaffleFinished = false,
                StartsAt = startsAt,
            }
        );

        var provider = _catalog.NormalProvider();
        await provider.ReloadAsync(Ct);
        var fakes = _catalog.Fakes;

        _snapshot = provider.Current;
        fakes.Handlers["GetCatalogSnapshot"] = _ => provider.Current;
        fakes.Handlers["GetAmountForCurrencyAsync"] = _ => Task.FromResult(100);
        fakes.Handlers["TryDebitAsync"] = _ => Task.FromResult(WalletDebitResult.Success());
        // Every entrant weighs the same unless a test says otherwise.
        fakes.Handlers["GetSummaryAsync"] = _ => Task.FromResult(Blank<PlayerSummarySnapshot>());
        fakes.Handlers["GetExtendedProfileSnapshotAsync"] = _ =>
            Task.FromResult(Blank<PlayerExtendedProfileSnapshot>());

        // Orleans finds the timer registry through the grain's context.
        fakes.Handlers["GetService"] = call =>
            call.Args[0] is Type t && t.IsInterface ? fakes.Create(t) : null;
        fakes.Handlers["RegisterGrainTimer"] = call =>
            _timer = new RaffleTimer(
                call.Args[1]!,
                call.Args[2],
                ((GrainTimerCreationOptions)call.Args[3]!).DueTime
            );

        var grain = GrainHarness.Create(
            typeof(CatalogModule).Assembly,
            "Turbo.Catalog.Grains.CatalogLtdRaffleGrain",
            fakes,
            _catalog.Db
        );

        RoomHarness.SetMember(RoomHarness.GetField(grain, "_state")!, "SeriesId", SERIES);
        RoomHarness.SetField(grain, "_catalogService", fakes.Create<ICatalogService>());
        typeof(Grain)
            .GetProperty(
                "GrainContext",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            )!
            .GetSetMethod(true)!
            .Invoke(grain, [fakes.Create<IGrainContext>("raffle")]);
        await ((Grain)grain).OnActivateAsync(Ct);

        _grain = grain;

        return (ICatalogLtdRaffleGrain)grain;
    }

    /// <summary>
    /// Only account age counts, one per day: the experienced weigh 30, the rest nothing, so the
    /// experienced are all drawn first.
    /// </summary>
    private void WeighByAccountAge(int[] experienced)
    {
        var weights = ((CatalogConfig)RoomHarness.GetField(_grain!, "_config")!).LtdRaffle;

        weights.BaseWeight = 0;
        foreach (
            var criterion in new[]
            {
                weights.BadgeCount,
                weights.RoomCount,
                weights.FurnitureCount,
                weights.FriendCount,
                weights.RespectsReceived,
                weights.AchievementScore,
            }
        )
            criterion.Enabled = false;
        weights.AccountAgeDays = new WeightCriterion
        {
            Enabled = true,
            BonusPerUnit = 1,
            MaxBonus = 100,
        };

        _catalog.Fakes.Handlers["GetSummaryAsync"] = call =>
        {
            var summary = Blank<PlayerSummarySnapshot>();
            var age = experienced.Contains((int)Convert.ToInt64(call.Key)) ? 30 : 0;

            RoomHarness.SetMember(summary, "CreatedAt", DateTime.UtcNow.AddDays(-age));

            return Task.FromResult(summary);
        };
    }

    /// <summary>The result each entrant was sent: whether they won.</summary>
    private IEnumerable<(int Player, bool Won)> Results() =>
        _catalog
            .Fakes.Log.Of("SendComposerAsync")
            .Where(x => x.Args[0] is LtdRaffleResultMessageComposer)
            .Select(x =>
                (
                    (int)Convert.ToInt64(x.Key),
                    ((LtdRaffleResultMessageComposer)x.Args[0]!).ResultCode
                        == LtdRaffleResultType.Won
                )
            );

    private async Task<List<LtdRaffleEntryEntity>> EntriesAsync()
    {
        await using var db = await _catalog.Db.CreateDbContextAsync(Ct);

        return await db.LtdRaffleEntries.AsNoTracking().ToListAsync(Ct);
    }

    private async Task<int> StockAsync()
    {
        await using var db = await _catalog.Db.CreateDbContextAsync(Ct);

        return (await db.LtdSeries.SingleAsync(Ct)).RemainingQuantity;
    }

    /// <summary>The emulator as the host builds it, with the real catalog service.</summary>
    private TurboEmulator Emulator()
    {
        var catalogCtor = typeof(CatalogService).GetConstructors().Single();
        var catalog = (ICatalogService)
            catalogCtor.Invoke(
                catalogCtor
                    .GetParameters()
                    .Select(p =>
                        p.ParameterType == typeof(IDbContextFactory<TurboDbContext>) ? _catalog.Db
                        : p.ParameterType == typeof(ILogger<ICatalogService>)
                            ? NullLogger<ICatalogService>.Instance
                        : p.ParameterType.IsGenericType
                        && p.ParameterType.GetGenericTypeDefinition() == typeof(IOptions<>)
                            ? OptionsOf(p.ParameterType.GetGenericArguments()[0])
                        : _catalog.Fakes.Create(p.ParameterType)
                    )
                    .ToArray()
            );
        var ctor = typeof(TurboEmulator).GetConstructors().Single();

        return (TurboEmulator)
            ctor.Invoke(
                ctor.GetParameters()
                    .Select(p =>
                        p.ParameterType == typeof(ICatalogService) ? catalog
                        : p.ParameterType == typeof(ILogger<TurboEmulator>)
                            ? NullLogger<TurboEmulator>.Instance
                        : _catalog.Fakes.Create(p.ParameterType)
                    )
                    .ToArray()
            );
    }

    private static object OptionsOf(Type type) =>
        typeof(Options)
            .GetMethod(nameof(Options.Create))!
            .MakeGenericMethod(type)
            .Invoke(null, [Activator.CreateInstance(type)])!;

    private static T Blank<T>() =>
        (T)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(T));

    /// <summary>
    /// A grain timer as Orleans runs one: its callback gets a token that disposing the timer
    /// cancels.
    /// </summary>
    private sealed class RaffleTimer(object callback, object? state, TimeSpan dueTime) : IGrainTimer
    {
        private readonly CancellationTokenSource _disposed = new();

        public TimeSpan DueTime { get; } = dueTime;

        public Task FireAsync() =>
            (Task)
                callback.GetType().GetMethod("Invoke")!.Invoke(callback, [state, _disposed.Token])!;

        public void Change(TimeSpan dueTime, TimeSpan period) { }

        public void Dispose() => _disposed.Cancel();
    }
}
