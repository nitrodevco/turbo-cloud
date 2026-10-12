using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Turbo.Database.Context;
using Turbo.Database.Entities.Room;

namespace Turbo.Database.Extensions;

/// <summary>
/// Writing chat lines, the busiest log the hotel keeps. On PostgreSQL a batch goes in as one
/// binary COPY, which PostgreSQL's own guidance (Populating a Database) recommends for loading
/// many rows: measured on <c>room_chat_logs</c> with its indexes, a batch of 200 took 10 ms by
/// COPY against 50 ms through EF's batched inserts, five times the rows for the same time on
/// the database and the server. A COPY is one statement, so a batch lands whole or not at all,
/// as the inserts did. Anywhere else (the tests' SQLite) it is EF's insert.
/// </summary>
public static class ChatlogWrites
{
    private const string COPY =
        "COPY room_chat_logs (room_id, player_id, target_player_id, message, typed_message, created_at) "
        + "FROM STDIN (FORMAT BINARY)";

    public static async Task InsertChatlogsAsync(
        this TurboDbContext db,
        IReadOnlyCollection<RoomChatlogEntity> lines,
        CancellationToken ct
    )
    {
        if (lines.Count == 0)
            return;

        if (!db.Database.IsNpgsql())
        {
            db.Chatlogs.AddRange(lines);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            return;
        }

        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);

        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var writer = await connection.BeginBinaryImportAsync(COPY, ct).ConfigureAwait(false);

        await using (writer.ConfigureAwait(false))
        {
            foreach (var line in lines)
            {
                await writer.StartRowAsync(ct).ConfigureAwait(false);
                await writer
                    .WriteAsync(line.RoomEntityId, NpgsqlDbType.Integer, ct)
                    .ConfigureAwait(false);
                await writer
                    .WriteAsync(line.PlayerEntityId, NpgsqlDbType.Integer, ct)
                    .ConfigureAwait(false);
                await WriteOrNullAsync(writer, line.TargetPlayerEntityId, NpgsqlDbType.Integer, ct)
                    .ConfigureAwait(false);
                await writer
                    .WriteAsync(line.Message, NpgsqlDbType.Varchar, ct)
                    .ConfigureAwait(false);
                await WriteOrNullAsync(writer, line.TypedMessage, NpgsqlDbType.Varchar, ct)
                    .ConfigureAwait(false);
                await writer
                    .WriteAsync(AsUtc(line.CreatedAt), NpgsqlDbType.TimestampTz, ct)
                    .ConfigureAwait(false);
            }

            await writer.CompleteAsync(ct).ConfigureAwait(false);
        }
    }

    private static Task WriteOrNullAsync<T>(
        NpgsqlBinaryImporter writer,
        T? value,
        NpgsqlDbType type,
        CancellationToken ct
    ) => value is null ? writer.WriteNullAsync(ct) : writer.WriteAsync(value, type, ct);

    /// <summary>A line stamped by the room is UTC; one without a time gets the moment it is written.</summary>
    private static DateTime AsUtc(DateTime at) =>
        at == default ? DateTime.UtcNow
        : at.Kind == DateTimeKind.Utc ? at
        : DateTime.SpecifyKind(at, DateTimeKind.Utc);
}
