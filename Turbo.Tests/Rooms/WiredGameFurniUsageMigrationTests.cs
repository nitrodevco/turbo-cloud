using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Migrations;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// <c>AlignWiredGameFurniUsage</c> gives the wired game furni Habbo's usage and state count (an
/// official client capture of 24 game rooms; the furni's own asset states), leaves a definition
/// staff changed alone, and undoes itself.
/// </summary>
public sealed class WiredGameFurniUsageMigrationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static FurnitureDefinitionEntity Definition(
        int id,
        string name,
        FurnitureUsageType usage,
        int states
    ) =>
        new()
        {
            Id = id,
            SpriteId = id,
            Name = name,
            ProductType = ProductType.Floor,
            FurniCategory = FurnitureCategory.Default,
            Logic = name,
            Width = 1,
            Length = 1,
            StackHeight = 0.2,
            CanStack = true,
            CanWalk = true,
            CanSit = false,
            CanLay = false,
            CanRecycle = true,
            CanTrade = true,
            CanGroup = true,
            CanSell = true,
            UsagePolicy = usage,
            TotalStates = states,
        };

    private static async Task RunAsync(SqliteDb db, IReadOnlyList<MigrationOperation> operations)
    {
        await using var ctx = await db.CreateDbContextAsync(Ct);

        foreach (var sql in operations.OfType<SqlOperation>())
            await ctx.Database.ExecuteSqlRawAsync(sql.Sql, Ct);
    }

    private static async Task<Dictionary<string, (FurnitureUsageType, int)>> ReadAsync(SqliteDb db)
    {
        await using var ctx = await db.CreateDbContextAsync(Ct);

        return await ctx
            .FurnitureDefinitions.AsNoTracking()
            .ToDictionaryAsync(x => x.Name, x => (x.UsagePolicy, x.TotalStates), Ct);
    }

    [Fact]
    public async Task Game_furni_get_habbos_usage_and_states_and_staff_choices_stand()
    {
        using var db = new SqliteDb();
        db.Insert(Definition(1, "wf_arrowplate", FurnitureUsageType.Controller, 3));
        db.Insert(Definition(2, "wf_tile1", FurnitureUsageType.Controller, 10));
        db.Insert(Definition(3, "wf_floor_switch1", FurnitureUsageType.Controller, 2));
        db.Insert(Definition(4, "wf_glowball", FurnitureUsageType.Controller, 3));
        db.Insert(Definition(5, "wf_colortile", FurnitureUsageType.Controller, 7));
        // Staff gave this one everyone's use: it is not on the shipped values, so it stays.
        db.Insert(Definition(6, "wf_ringplate", FurnitureUsageType.Everybody, 3));

        var migration = new AlignWiredGameFurniUsage();

        await RunAsync(db, migration.UpOperations);
        await RunAsync(db, migration.UpOperations);

        var after = await ReadAsync(db);

        after["wf_arrowplate"].Should().Be((FurnitureUsageType.Nobody, 2));
        after["wf_tile1"].Should().Be((FurnitureUsageType.Nobody, 10));
        after["wf_floor_switch1"].Should().Be((FurnitureUsageType.Everybody, 2));
        after["wf_glowball"].Should().Be((FurnitureUsageType.Controller, 2));
        after["wf_colortile"].Should().Be((FurnitureUsageType.Controller, 7));
        after["wf_ringplate"].Should().Be((FurnitureUsageType.Everybody, 3));

        await RunAsync(db, migration.DownOperations);

        var undone = await ReadAsync(db);

        undone["wf_arrowplate"].Should().Be((FurnitureUsageType.Controller, 3));
        undone["wf_tile1"].Should().Be((FurnitureUsageType.Controller, 10));
        undone["wf_floor_switch1"].Should().Be((FurnitureUsageType.Controller, 2));
        undone["wf_glowball"].Should().Be((FurnitureUsageType.Controller, 3));
        undone["wf_ringplate"].Should().Be((FurnitureUsageType.Everybody, 3));
    }
}
