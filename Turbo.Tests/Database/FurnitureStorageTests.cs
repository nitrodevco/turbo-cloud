using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Turbo.Database.Entities.Furniture;
using Xunit;

namespace Turbo.Tests.Database;

/// <summary>
/// How furni is stored for the room's saves, which rewrite where every furni stands and its state
/// all day. PostgreSQL rewrites such a row in place, with no new index entries (a HOT update),
/// only when no index holds a column that changed. Measured on 2 million furni:
/// <list type="bullet">
/// <item>An inventory index covering the load's columns (extra_data among them) made saves that
/// change a furni's state 40% slower, and once rooms had saved, the loads still read 13,403 of
/// 20,000 rows from the table, so the load was 25% faster rather than 99%.</item>
/// <item>A room index covering every column made position saves 83% slower (none in place
/// against 49%), tripled the indexes, and did not make a room load any faster.</item>
/// </list>
/// So no index on these tables holds a column a room save writes, and <c>furniture</c> leaves a
/// fifth of each page free for those in-place updates.
/// </summary>
public sealed class FurnitureStorageTests
{
    private static readonly string[] SavedByTheRoom =
    [
        "x",
        "y",
        "z",
        "direction",
        "wall_offset",
        "extra_data",
    ];

    [Theory]
    [InlineData(typeof(FurnitureEntity))]
    [InlineData(typeof(BuildersClubFurnitureEntity))]
    public void No_index_holds_a_column_a_room_save_writes(Type entity)
    {
        using var db = CoreMigrationsTests.OfflineContext();
        var model = db.GetService<IDesignTimeModel>().Model;
        var type = model.FindEntityType(entity)!;

        var offending = type.GetIndexes()
            .Where(index => index.GetMethod() != "brin")
            .SelectMany(index =>
                index
                    .Properties.Concat(
                        index.GetIncludeProperties() is { } included
                            ? included.Select(name => type.FindProperty(name)!)
                            : []
                    )
                    .Select(property =>
                        (Index: index.GetDatabaseName(), Column: property.GetColumnName())
                    )
            )
            .Where(x => SavedByTheRoom.Contains(x.Column))
            .ToList();

        offending
            .Should()
            .BeEmpty("an index on a column the room saves stops every such save updating in place");
    }

    [Fact]
    public void Furniture_leaves_room_on_each_page_for_in_place_updates()
    {
        using var db = CoreMigrationsTests.OfflineContext();
        var model = db.GetService<IDesignTimeModel>().Model;

        model
            .FindEntityType(typeof(FurnitureEntity))!
            .FindAnnotation("Npgsql:StorageParameter:fillfactor")
            ?.Value.Should()
            .Be(80);
    }
}
