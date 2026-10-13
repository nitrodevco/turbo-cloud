using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Turbo.Database.Context;
using Turbo.Database.Entities.Players;
using Xunit;

namespace Turbo.Tests.Database;

/// <summary>
/// Navigator search codes are compared case-sensitively, as on Habbo: its own room category list
/// (hhus, 2026-10-13) holds both "Trading" (14) and "TRADING" (13), so a new player is seeded
/// with the collapsed codes <c>category__Trading</c> and <c>category__TRADING</c>. Under MySQL's
/// default case-insensitive collation the (player, search code) unique index refused the second
/// row, the preferences flush failed and retried every tick, and the player's navigator
/// preferences never saved.
/// </summary>
public sealed class NavigatorSearchCodeCollationTests
{
    private const string UNREACHABLE = "server=127.0.0.1;port=1;user=x;password=x;database=turbo";

    private static TurboDbContext OfflineContext() =>
        new(
            new DbContextOptionsBuilder<TurboDbContext>()
                .UseMySql(
                    UNREACHABLE,
                    new MySqlServerVersion(new Version(8, 4, 0)),
                    mysql => mysql.MigrationsAssembly("Turbo.Database")
                )
                .Options
        );

    [Theory]
    [InlineData(typeof(PlayerNavigatorCollapsedCategoryEntity))]
    [InlineData(typeof(PlayerNavigatorViewModeEntity))]
    public void A_search_code_column_tells_Trading_and_TRADING_apart(Type entity)
    {
        using var db = OfflineContext();

        db.GetService<IDesignTimeModel>()
            .Model.FindEntityType(entity)!
            .FindProperty("SearchCode")!
            .GetCollation()
            .Should()
            .Be("utf8mb4_bin", "Habbo has categories that differ only in case");
    }
}
