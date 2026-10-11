using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Catalog;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// A limited series sold outright, once its raffle is over, through the series' grain. Only a
/// raffle's winners were recorded, so the serials free to sell were all of them every time: two
/// buyers of a two-piece series could both get serial 1, and one buyer could buy every piece.
/// </summary>
public sealed class LtdSaleTests : IDisposable
{
    private const int SERIES = 9;
    private const int FIRST_BUYER = 7;
    private const int SECOND_BUYER = 8;

    private readonly CatalogFixture _catalog = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public LtdSaleTests()
    {
        foreach (var id in new[] { FIRST_BUYER, SECOND_BUYER })
            _catalog.Db.Insert(
                new PlayerEntity
                {
                    Id = id,
                    Name = $"buyer-{id}",
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
                RaffleWindowSeconds = 0,
                IsActive = true,
                IsRaffleFinished = true,
            }
        );
    }

    public void Dispose() => _catalog.Dispose();

    [Fact]
    public async Task Each_piece_sold_outright_has_its_own_serial_and_the_sale_is_recorded()
    {
        var grain = await SeriesGrainAsync();

        (await grain.EnterRaffleAsync(FIRST_BUYER, Ct)).Success.Should().BeTrue();
        (await grain.EnterRaffleAsync(SECOND_BUYER, Ct)).Success.Should().BeTrue();

        await using var db = await _catalog.Db.CreateDbContextAsync(Ct);
        var sales = await db.LtdRaffleEntries.AsNoTracking().OrderBy(x => x.Id).ToListAsync(Ct);

        sales
            .Select(x => (x.PlayerEntityId, x.Result, x.BatchId))
            .Should()
            .Equal(
                (FIRST_BUYER, LtdEntryResult.Won, (Guid?)null),
                (SECOND_BUYER, LtdEntryResult.Won, null)
            );
        sales.Select(x => x.SerialNumber).Should().BeEquivalentTo([1, 2]);
        (await db.LtdSeries.SingleAsync(Ct)).RemainingQuantity.Should().Be(0);
    }

    /// <summary>One per customer, the hotel's default, counts a piece bought outright too.</summary>
    [Fact]
    public async Task A_buyer_who_has_a_piece_cannot_buy_another()
    {
        var grain = await SeriesGrainAsync();

        (await grain.EnterRaffleAsync(FIRST_BUYER, Ct)).Success.Should().BeTrue();
        var again = await grain.EnterRaffleAsync(FIRST_BUYER, Ct);

        again.Success.Should().BeFalse();
        again.Error.Should().Be(LtdRaffleEntryErrorType.AlreadyWon);
    }

    private async Task<ICatalogLtdRaffleGrain> SeriesGrainAsync()
    {
        var provider = _catalog.NormalProvider();
        await provider.ReloadAsync(Ct);
        var fakes = _catalog.Fakes;

        fakes.Handlers["GetCatalogSnapshot"] = _ => provider.Current;
        fakes.Handlers["GetAmountForCurrencyAsync"] = _ => Task.FromResult(100);
        fakes.Handlers["TryDebitAsync"] = _ => Task.FromResult(WalletDebitResult.Success());

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
        await ((Grain)grain).OnActivateAsync(Ct);

        return (ICatalogLtdRaffleGrain)grain;
    }
}
