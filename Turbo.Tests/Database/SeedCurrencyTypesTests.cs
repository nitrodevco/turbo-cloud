using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Migrations;
using Turbo.Players.Providers;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Wallet;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Database;

/// <summary>
/// A hotel installed from nothing (2026-10-29) had no row in <c>currency_types</c>: the purse
/// showed zeros, the catalog could charge nothing and <c>:give</c> knew no currency. The seed
/// gives it the three the client draws, and leaves a hotel's own rows alone.
/// </summary>
public sealed class SeedCurrencyTypesTests : IDisposable
{
    private readonly SqliteDb _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task An_empty_hotel_gets_credits_duckets_and_diamonds_the_wallet_can_name()
    {
        await ApplyAsync();

        var provider = new CurrencyTypeProvider(_db, NullLogger<ICurrencyTypeProvider>.Instance);

        await provider.ReloadAsync(Ct);

        provider.TryGetCurrencyKindByName("credits", out var credits).Should().BeTrue();
        credits.CurrencyType.Should().Be(CurrencyType.Credits);
        provider.TryGetCurrencyKindByName("duckets", out var duckets).Should().BeTrue();
        duckets
            .Should()
            .Be(
                new CurrencyKind
                {
                    CurrencyType = CurrencyType.ActivityPoints,
                    ActivityPointType = 0,
                }
            );
        provider.TryGetCurrencyKindByName("diamonds", out var diamonds).Should().BeTrue();
        diamonds
            .Should()
            .Be(
                new CurrencyKind
                {
                    CurrencyType = CurrencyType.ActivityPoints,
                    ActivityPointType = 5,
                }
            );
        provider.GetEnabledCurrencyNames().Should().HaveCount(3);
    }

    [Fact]
    public async Task Applying_it_again_adds_nothing_and_a_hotels_own_row_of_a_kind_is_kept()
    {
        _db.Insert(
            new CurrencyTypeEntity
            {
                Id = 1,
                Name = "Diamonds",
                CurrencyType = CurrencyType.ActivityPoints,
                ActivityPointType = 5,
                Enabled = false,
            }
        );

        await ApplyAsync();
        await ApplyAsync();

        await using var db = await _db.CreateDbContextAsync(Ct);
        var rows = (await db.CurrencyTypes.OrderBy(x => x.Id).ToListAsync(Ct)).Select(x =>
            (x.Name, x.CurrencyType, x.ActivityPointType, x.Enabled)
        );

        rows.Should()
            .Equal(
                ("Diamonds", CurrencyType.ActivityPoints, 5, false),
                ("credits", CurrencyType.Credits, null, true),
                ("duckets", CurrencyType.ActivityPoints, 0, true)
            );
    }

    [Fact]
    public void Each_row_is_guarded_by_its_kind_and_nothing_is_taken_back()
    {
        Sql("Up")
            .Should()
            .HaveCount(3)
            .And.OnlyContain(x => x.Contains("WHERE NOT EXISTS"))
            .And.Contain(x => x.Contains("'credits', 1, NULL") && x.Contains("IS NULL"))
            .And.Contain(x => x.Contains("'duckets', 4, 0") && x.Contains("= 0"))
            .And.Contain(x => x.Contains("'diamonds', 4, 5") && x.Contains("= 5"));
        Sql("Down").Should().BeEmpty();
    }

    private async Task ApplyAsync()
    {
        await using var db = await _db.CreateDbContextAsync(Ct);

        foreach (var sql in Sql("Up"))
            await db.Database.ExecuteSqlRawAsync(sql, Ct);
    }

    private static List<string> Sql(string method)
    {
        var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");

        typeof(SeedCurrencyTypes)
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(new SeedCurrencyTypes(), [builder]);

        return [.. builder.Operations.OfType<SqlOperation>().Select(x => x.Sql)];
    }
}
