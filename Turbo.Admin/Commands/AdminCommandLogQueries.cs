using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Configuration;
using Turbo.Database.Context;

namespace Turbo.Admin.Commands;

/// <summary>
/// The command log as staff read it: newest first, a page at a time, narrowed to one player (by
/// name or id), one command, one outcome or one source. Read-only, with the names of the players
/// and rooms on the page looked up in one query each.
/// </summary>
public sealed class AdminCommandLogQueries(
    IDbContextFactory<TurboDbContext> database,
    IOptions<AdminConfig> config
)
{
    /// <summary>The source asked for to mean a command typed in a room's chat, which has none.</summary>
    public const string CHAT_SOURCE = "chat";

    public async Task<CommandLogResponse> SearchAsync(
        string? player,
        string? command,
        string? outcome,
        string? source,
        int page,
        CancellationToken ct
    )
    {
        var pageSize = Math.Max(1, config.Value.CommandLogPageSize);

        page = Math.Max(1, page);

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var logs = db.CommandLogs.AsNoTracking();
        var who = (player ?? string.Empty).Trim();

        if (who.Length > 0)
        {
            int? playerId = int.TryParse(who, out var id)
                ? id
                : await db
                    .Players.AsNoTracking()
                    .Where(x => x.Name == who)
                    .Select(x => (int?)x.Id)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);

            if (playerId is not { } found)
                return new CommandLogResponse(0, page, pageSize, []);

            logs = logs.Where(x => x.PlayerEntityId == found);
        }

        // As typed in game or as listed: ":ban" and "Ban" both mean the ban command.
        var name = (command ?? string.Empty).Trim().TrimStart(':').ToLowerInvariant();

        if (name.Length > 0)
            logs = logs.Where(x => x.Command == name);

        var result = (outcome ?? string.Empty).Trim();

        if (result.Length > 0)
            logs = logs.Where(x => x.Outcome == result);

        var from = (source ?? string.Empty).Trim();

        if (from == CHAT_SOURCE)
            logs = logs.Where(x => x.Source == null);
        else if (from.Length > 0)
            logs = logs.Where(x => x.Source == from);

        var total = await logs.CountAsync(ct).ConfigureAwait(false);
        var rows = await logs.OrderByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.CreatedAt,
                x.PlayerEntityId,
                x.RoomEntityId,
                x.Command,
                x.Arguments,
                x.Outcome,
                x.Source,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var playerIds = rows.Select(x => x.PlayerEntityId).Where(x => x > 0).Distinct().ToList();
        var roomIds = rows.Select(x => x.RoomEntityId).Where(x => x > 0).Distinct().ToList();
        var players = await db
            .Players.AsNoTracking()
            .Where(x => playerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct)
            .ConfigureAwait(false);
        var rooms = await db
            .Rooms.AsNoTracking()
            .Where(x => roomIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct)
            .ConfigureAwait(false);

        return new CommandLogResponse(
            total,
            page,
            pageSize,
            [
                .. rows.Select(x => new CommandLogEntry(
                    x.Id,
                    x.CreatedAt,
                    x.PlayerEntityId,
                    players.GetValueOrDefault(x.PlayerEntityId),
                    x.RoomEntityId,
                    rooms.GetValueOrDefault(x.RoomEntityId),
                    x.Command,
                    x.Arguments,
                    x.Outcome,
                    x.Source
                )),
            ]
        );
    }
}
