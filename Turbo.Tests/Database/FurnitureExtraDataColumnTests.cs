using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Turbo.Database.Context;
using Turbo.Database.Entities.Furniture;
using Xunit;

namespace Turbo.Tests.Database;

/// <summary>
/// A placed furni's extra data holds a wired box's whole config. A Show Message box with
/// Habbo's message-editor text (up to 400 characters, plus placeholders such as
/// <c>$(trigger_text)</c>, written as JSON) is longer than 512 characters: with
/// <c>varchar(512)</c> MySQL refused the row ("Data too long for column 'extra_data'"), the
/// room's whole dirty-furni batch failed and stayed queued, and no furni change in that room was
/// ever written again - after a restart its wired boxes came back empty (local copy of the
/// official starter room, 2026-10-13: 15 of 17 saved boxes lost).
/// </summary>
public sealed class FurnitureExtraDataColumnTests
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
    [InlineData(typeof(FurnitureEntity))]
    [InlineData(typeof(BuildersClubFurnitureEntity))]
    public void A_wired_boxs_whole_config_fits_its_extra_data_column(Type entity)
    {
        using var db = OfflineContext();

        db.GetService<IDesignTimeModel>()
            .Model.FindEntityType(entity)!
            .FindProperty("ExtraData")!
            .GetColumnType()
            .Should()
            .Be(
                "longtext",
                "a Show Message config with a 400-character text is over 512 characters"
            );
    }
}
