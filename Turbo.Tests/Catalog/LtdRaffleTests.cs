using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Orleans.Runtime;
using Turbo.Catalog;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// A limited series sold by raffle, through the series' grain: entries are taken for the
/// raffle's window, then its timer draws the winners. The draw disposed its own timer first,
/// which cancels the token the timer hands it, so the draw's next query was cancelled after the
/// raffle was marked finished: on a real server, 50 entrants and a 30 s window left 0 winners,
/// 50 entries pending and the whole stock unsold.
/// </summary>
public sealed class LtdRaffleTests : IDisposable
{
    private const int SERIES = 9;
    private static readonly int[] Entrants = [7, 8, 9];

    private readonly CatalogFixture _catalog = new();
    private RaffleTimer? _timer;

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

        _catalog.Db.Insert(
            new LtdSeriesEntity
            {
                Id = SERIES,
                CatalogProductEntityId = SOLD,
                TotalQuantity = 2,
                RemainingQuantity = 2,
                RaffleWindowSeconds = 30,
                IsActive = true,
                IsRaffleFinished = false,
            }
        );
    }

    public void Dispose() => _catalog.Dispose();

    [Fact]
    public async Task When_its_window_ends_the_raffle_draws_its_winners()
    {
        var grain = await SeriesGrainAsync();

        foreach (var id in Entrants)
            (await grain.EnterRaffleAsync(id, Ct)).Success.Should().BeTrue();

        await _timer!.FireAsync();

        await using var db = await _catalog.Db.CreateDbContextAsync(Ct);
        var entries = await db.LtdRaffleEntries.AsNoTracking().ToListAsync(Ct);

        entries.Count(x => x.Result == LtdEntryResult.Won).Should().Be(2);
        entries.Count(x => x.Result == LtdEntryResult.Lost).Should().Be(1);
        entries
            .Where(x => x.Result == LtdEntryResult.Won)
            .Select(x => x.SerialNumber)
            .Should()
            .BeEquivalentTo([1, 2]);
        (await db.LtdSeries.SingleAsync(Ct)).RemainingQuantity.Should().Be(0);
        _catalog.Fakes.Log.Of(nameof(IInventoryGrain.GrantLtdFurnitureAsync)).Should().HaveCount(2);
    }

    private async Task<ICatalogLtdRaffleGrain> SeriesGrainAsync()
    {
        var provider = _catalog.NormalProvider();
        await provider.ReloadAsync(Ct);
        var fakes = _catalog.Fakes;

        fakes.Handlers["GetCatalogSnapshot"] = _ => provider.Current;
        fakes.Handlers["GetAmountForCurrencyAsync"] = _ => Task.FromResult(100);
        fakes.Handlers["TryDebitAsync"] = _ => Task.FromResult(WalletDebitResult.Success());
        // Every entrant weighs the same.
        fakes.Handlers["GetSummaryAsync"] = _ =>
            Task.FromResult(Blank<Turbo.Primitives.Players.Snapshots.PlayerSummarySnapshot>());
        fakes.Handlers["GetExtendedProfileSnapshotAsync"] = _ =>
            Task.FromResult(
                Blank<Turbo.Primitives.Players.Snapshots.PlayerExtendedProfileSnapshot>()
            );

        // Orleans finds the timer registry through the grain's context.
        fakes.Handlers["GetService"] = call =>
            call.Args[0] is Type t && t.IsInterface ? fakes.Create(t) : null;
        fakes.Handlers["RegisterGrainTimer"] = call =>
            _timer = new RaffleTimer(call.Args[1]!, call.Args[2]);

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
        grain
            .GetType()
            .GetField("_catalogService", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(grain, fakes.Create<ICatalogService>());
        typeof(Grain)
            .GetProperty(
                "GrainContext",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            )!
            .GetSetMethod(true)!
            .Invoke(grain, [fakes.Create<IGrainContext>("raffle")]);
        await ((Grain)grain).OnActivateAsync(Ct);

        return (ICatalogLtdRaffleGrain)grain;
    }

    private static T Blank<T>() =>
        (T)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(T));

    /// <summary>
    /// A grain timer as Orleans runs one: its callback gets a token that disposing the timer
    /// cancels.
    /// </summary>
    private sealed class RaffleTimer(object callback, object? state) : IGrainTimer
    {
        private readonly CancellationTokenSource _disposed = new();

        public Task FireAsync() =>
            (Task)
                callback.GetType().GetMethod("Invoke")!.Invoke(callback, [state, _disposed.Token])!;

        public void Change(TimeSpan dueTime, TimeSpan period) { }

        public void Dispose() => _disposed.Cancel();
    }
}
