using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Turbo.Database.Context;
using Turbo.Database.Entities.Furniture;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Rooms.Grains;

/// <summary>
/// Where a furni is, decided by the database the moment it moves between an inventory and a
/// room, not at the room's next save. A furni is in one place: an inventory, a room, or held
/// by another furni (a chest, a jukebox, a present). Every move out of one place is a
/// conditional write on the place it is leaving, so of two moves that race (a placement and a
/// deposit, a trade and a placement, a pickup in one room and a placement in another) exactly
/// one wins, and the other is refused rather than leaving the furni in two places.
/// </summary>
internal static class FurnitureLocations
{
    /// <summary>
    /// Puts an inventory furni into a room: only while it is still in its owner's inventory.
    /// False when it is not (traded, deposited, placed somewhere else), and the caller refuses
    /// the placement. The height is the room's to work out, and its next save writes it.
    /// </summary>
    public static async Task<bool> ClaimForRoomAsync(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        int itemId,
        int ownerId,
        int roomId,
        int x,
        int y,
        Rotation rotation,
        double? z,
        int? wallOffset,
        CancellationToken ct
    )
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        var claimed = await dbCtx
            .Furnitures.Where(f =>
                f.Id == itemId
                && f.PlayerEntityId == ownerId
                && f.RoomEntityId == null
                && f.ChestItemEntityId == null
            )
            .ExecuteUpdateAsync(
                up =>
                    up.SetProperty(f => f.RoomEntityId, roomId)
                        .SetProperty(f => f.X, x)
                        .SetProperty(f => f.Y, y)
                        .SetProperty(f => f.Rotation, rotation)
                        .SetProperty(f => f.Z, f => z ?? f.Z)
                        .SetProperty(f => f.WallOffset, f => wallOffset ?? f.WallOffset),
                ct
            );

        return claimed == 1;
    }

    /// <summary>
    /// Takes furni out of a room into their owners' inventories, or into
    /// <paramref name="newOwnerId"/>'s (a staff member keeping what they pick up, recorded as a
    /// transfer), with the data they leave the room with: only those the database still has in
    /// this room. Returns those ids; one that is not there is refused, so
    /// it is not listed twice.
    /// </summary>
    public static async Task<HashSet<int>> ReleaseFromRoomAsync(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IReadOnlyCollection<RoomItemSnapshot> items,
        int roomId,
        int? newOwnerId,
        CancellationToken ct
    )
    {
        var released = new HashSet<int>();

        if (items.Count == 0)
            return released;

        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);
        await using var transaction = await dbCtx.Database.BeginTransactionAsync(ct);

        foreach (var chunk in items.Chunk(ROWS_PER_STATEMENT))
        {
            await using var command = await RowsCommandAsync(
                dbCtx,
                chunk,
                row => [row.ObjectId.Value, row.ExtraData],
                [DbType.Int32, DbType.String],
                ct
            );

            command.CommandText = $"""
                UPDATE furniture
                SET room_id = NULL,
                    extra_data = v.column2,
                    player_id = COALESCE({Parameter(command, newOwnerId, DbType.Int32)}, player_id)
                FROM ({command.CommandText}) AS v
                WHERE furniture.id = v.column1
                  AND furniture.room_id = {Parameter(command, roomId, DbType.Int32)}
                RETURNING furniture.id
                """;

            await using var reader = await command.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
                released.Add(reader.GetInt32(0));
        }

        // A furni handed to someone other than its owner changed hands, in its history too.
        if (newOwnerId is { } to)
        {
            dbCtx.FurnitureTransfers.AddRange(
                items
                    .Where(x => released.Contains(x.ObjectId.Value) && x.OwnerId.Value != to)
                    .Select(x => new FurnitureTransferEntity
                    {
                        FurnitureId = x.ObjectId.Value,
                        DefinitionId = x.DefinitionId,
                        FromPlayerId = x.OwnerId.Value,
                        ToPlayerId = to,
                        Reason = FurnitureTransferReason.StaffPickup,
                    })
            );

            await dbCtx.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);

        return released;
    }

    /// <summary>
    /// Writes where furni stand in a room and their data, one statement for the whole batch:
    /// only rows the database has in this room. A row that has left it (picked up, deleted,
    /// placed elsewhere since) is not matched, so it is neither moved back into the room nor
    /// able to fail the write of the others. A hotel furni is keyed by its id; a borrowed one
    /// by its id in the room.
    /// </summary>
    public static async Task WritePlacementsAsync(
        TurboDbContext dbCtx,
        int roomId,
        IReadOnlyCollection<RoomItemSnapshot> items,
        bool borrowed,
        CancellationToken ct
    )
    {
        var (table, key) = borrowed
            ? ("furniture_builders_club", "room_object_id")
            : ("furniture", "id");

        foreach (var chunk in items.Chunk(ROWS_PER_STATEMENT))
        {
            await using var command = await RowsCommandAsync(
                dbCtx,
                chunk,
                row =>
                    [
                        row.ObjectId.Value,
                        row.X,
                        row.Y,
                        row.Z.Value,
                        (int)row.Rotation,
                        (row as RoomWallItemSnapshot)?.WallOffset,
                        row.ExtraData,
                    ],
                [
                    DbType.Int32,
                    DbType.Int32,
                    DbType.Int32,
                    DbType.Double,
                    DbType.Int32,
                    DbType.Int32,
                    DbType.String,
                ],
                ct
            );

            command.CommandText = $"""
                UPDATE {table}
                SET x = v.column2,
                    y = v.column3,
                    z = v.column4,
                    direction = v.column5,
                    wall_offset = COALESCE(v.column6, wall_offset),
                    extra_data = v.column7
                FROM ({command.CommandText}) AS v
                WHERE {table}.{key} = v.column1
                  AND {table}.room_id = {Parameter(command, roomId, DbType.Int32)}
                """;

            await command.ExecuteNonQueryAsync(ct);
        }
    }

    /// <summary>
    /// Rows a statement takes at once: well inside both PostgreSQL's 65,535 parameters and
    /// SQLite's 32,766, at seven a row.
    /// </summary>
    private const int ROWS_PER_STATEMENT = 500;

    /// <summary>
    /// A command on the context's connection whose text is a VALUES list of the rows, typed
    /// parameters each (a null is still typed, which PostgreSQL needs to read it); the caller
    /// wraps the text in its statement. VALUES names its columns column1, column2, ... on
    /// PostgreSQL and SQLite alike.
    /// </summary>
    private static async Task<DbCommand> RowsCommandAsync<T>(
        TurboDbContext dbCtx,
        IReadOnlyList<T> rows,
        Func<T, object?[]> values,
        DbType[] types,
        CancellationToken ct
    )
    {
        await dbCtx.Database.OpenConnectionAsync(ct);

        var command = dbCtx.Database.GetDbConnection().CreateCommand();
        command.Transaction = dbCtx.Database.CurrentTransaction?.GetDbTransaction();

        var text = new StringBuilder("VALUES ");

        for (var i = 0; i < rows.Count; i++)
        {
            var row = values(rows[i]);

            text.Append(i == 0 ? "(" : ", (");

            for (var c = 0; c < row.Length; c++)
                text.Append(c == 0 ? "" : ", ").Append(Parameter(command, row[c], types[c]));

            text.Append(')');
        }

        command.CommandText = text.ToString();

        return command;
    }

    private static string Parameter(DbCommand command, object? value, DbType type)
    {
        var parameter = command.CreateParameter();

        parameter.ParameterName = $"@p{command.Parameters.Count}";
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;

        command.Parameters.Add(parameter);

        return parameter.ParameterName;
    }

    /// <summary>Undoes a claim the room then refused (a limit, a spot taken meanwhile).</summary>
    public static async Task UnclaimAsync(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        int itemId,
        int roomId,
        CancellationToken ct
    )
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        await dbCtx
            .Furnitures.Where(f => f.Id == itemId && f.RoomEntityId == roomId)
            .ExecuteUpdateAsync(up => up.SetProperty(f => f.RoomEntityId, (int?)null), ct);
    }
}
