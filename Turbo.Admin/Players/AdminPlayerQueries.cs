using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Configuration;
using Turbo.Database.Context;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Admin.Players;

/// <summary>
/// Players as staff look them up: everyone, by part of their name or by id, the online ones first
/// when asked, and one player in detail. Read-only: the rows are read as the hotel left them, who
/// is online comes from the open sessions, and only an online player's presence is asked which
/// room they are in, so looking at a player never starts anything for them.
/// </summary>
public sealed class AdminPlayerQueries(
    IDbContextFactory<TurboDbContext> database,
    IGrainFactory grainFactory,
    ISessionGateway sessions,
    IOptions<AdminConfig> config,
    TimeProvider timeProvider
)
{
    private const string ESCAPE = "\\";

    /// <summary>The rooms a player's page lists, most recently active first.</summary>
    private const int RECENT_ROOMS = 8;

    /// <summary>The sanctions a player's page lists, newest first.</summary>
    private const int RECENT_SANCTIONS = 20;

    public async Task<PlayerListResponse> SearchAsync(
        string? text,
        PlayerSearchMode mode,
        bool onlineOnly,
        int page,
        CancellationToken ct
    )
    {
        var pageSize = Math.Max(1, config.Value.PlayerSearchPageSize);
        var term = (text ?? string.Empty).Trim();

        if (term.Length > config.Value.RoomSearchMaxLength)
            term = term[..config.Value.RoomSearchMaxLength];

        page = Math.Max(1, page);

        var online = sessions.GetOnlinePlayerIds().Select(x => x.Value).ToHashSet();
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var players = db.Players.AsNoTracking();

        if (onlineOnly)
        {
            var ids = online.ToList();

            players = players.Where(x => ids.Contains(x.Id));
        }

        if (term.Length > 0)
        {
            // LIKE, so case is ignored the same way on every database; the text is escaped so a
            // % or _ in it is matched as itself.
            var like = term.Replace(ESCAPE, ESCAPE + ESCAPE, StringComparison.Ordinal)
                .Replace("%", ESCAPE + "%", StringComparison.Ordinal)
                .Replace("_", ESCAPE + "_", StringComparison.Ordinal);

            players = mode switch
            {
                PlayerSearchMode.Id => int.TryParse(term, out var id)
                    ? players.Where(x => x.Id == id)
                    : players.Where(x => false),
                PlayerSearchMode.Discord => players.Where(x =>
                    db.PlayerDiscordLinks.Any(l =>
                        l.PlayerEntityId == x.Id
                        && (
                            l.DiscordId == term
                            || EF.Functions.Like(l.DiscordUsername, "%" + like + "%", ESCAPE)
                        )
                    )
                ),
                _ => players.Where(x => EF.Functions.Like(x.Name, "%" + like + "%", ESCAPE)),
            };
        }

        var total = await players.CountAsync(ct).ConfigureAwait(false);
        var rows = await players
            // Who logged in last first; players who never have go last.
            .OrderByDescending(x => x.LastLoginAt != null)
            .ThenByDescending(x => x.LastLoginAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Motto,
                x.LastLoginAt,
                x.CreatedAt,
                RoomsOwned = x.Rooms!.Count,
                DiscordUsername = db
                    .PlayerDiscordLinks.Where(l => l.PlayerEntityId == x.Id)
                    .Select(l => l.DiscordUsername)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new PlayerListResponse(
            total,
            page,
            pageSize,
            online.Count,
            [
                .. rows.Select(x => new PlayerListItem(
                    x.Id,
                    x.Name,
                    string.IsNullOrWhiteSpace(x.Motto) ? null : x.Motto,
                    online.Contains(x.Id),
                    x.LastLoginAt,
                    x.CreatedAt,
                    x.RoomsOwned,
                    x.DiscordUsername
                )),
            ]
        );
    }

    /// <summary>One player in detail; null when there is no such player.</summary>
    public async Task<PlayerDetailResponse?> GetAsync(int playerId, CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var player = await db
            .Players.AsNoTracking()
            .Where(x => x.Id == playerId)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Motto,
                x.Figure,
                x.Gender,
                x.LastLoginAt,
                x.CreatedAt,
                x.RespectPoints,
                RoomsOwned = x.Rooms!.Count,
            })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (player is null)
            return null;

        var currencies = await db
            .PlayerCurrencies.AsNoTracking()
            .Where(x => x.PlayerEntityId == playerId)
            .OrderBy(x => x.CurrencyTypeEntityId)
            .Select(x => new
            {
                TypeId = x.CurrencyTypeEntityId,
                x.CurrencyTypeEntity!.Name,
                x.CurrencyTypeEntity.CurrencyType,
                x.Amount,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var recentRooms = await db
            .Rooms.AsNoTracking()
            .Where(x => x.PlayerEntityId == playerId)
            .OrderByDescending(x => x.LastActive)
            .Take(RECENT_ROOMS)
            .Select(x => new PlayerRoomRef(x.Id, x.Name))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var sanctions = await db
            .PlayerSanctions.AsNoTracking()
            .Where(x => x.PlayerEntityId == playerId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(RECENT_SANCTIONS)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // The staff named on the sanctions, in one lookup.
        var staffIds = sanctions
            .SelectMany(x => new[] { x.IssuerEntityId, x.RevokedByEntityId })
            .OfType<int>()
            .Distinct()
            .ToList();
        var staffNames = await db
            .Players.AsNoTracking()
            .Where(x => staffIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct)
            .ConfigureAwait(false);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var discord = await db
            .PlayerDiscordLinks.AsNoTracking()
            .Where(x => x.PlayerEntityId == playerId)
            .Select(x => new
            {
                x.DiscordId,
                x.DiscordUsername,
                x.CreatedAt,
            })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        var signIns = discord is null
            ? 0
            : await db
                .WebSessions.CountAsync(x => x.PlayerEntityId == playerId && x.ExpiresAt > now, ct)
                .ConfigureAwait(false);

        var isOnline = sessions.GetOnlinePlayerIds().Any(x => x.Value == playerId);
        var currentRoom = isOnline
            ? await CurrentRoomAsync(db, playerId, ct).ConfigureAwait(false)
            : null;

        return new PlayerDetailResponse(
            player.Id,
            player.Name,
            string.IsNullOrWhiteSpace(player.Motto) ? null : player.Motto,
            player.Figure,
            player.Gender.ToString(),
            isOnline,
            currentRoom,
            player.LastLoginAt,
            player.CreatedAt,
            player.RespectPoints,
            [
                .. currencies.Select(x => new PlayerCurrencyItem(
                    x.TypeId,
                    string.IsNullOrWhiteSpace(x.Name) ? x.CurrencyType.ToString() : x.Name,
                    x.Amount
                )),
            ],
            player.RoomsOwned,
            recentRooms,
            [
                .. sanctions.Select(x => new PlayerSanctionItem(
                    x.Kind.ToString(),
                    x.Reason,
                    NameOf(staffNames, x.IssuerEntityId),
                    x.CreatedAt,
                    x.ExpiresAt,
                    x.RevokedAt,
                    NameOf(staffNames, x.RevokedByEntityId),
                    x.RevokedAt is null && (x.ExpiresAt is null || x.ExpiresAt > now)
                )),
            ],
            discord is null
                ? null
                : new PlayerDiscordInfo(
                    discord.DiscordId,
                    discord.DiscordUsername,
                    discord.CreatedAt,
                    signIns
                )
        );
    }

    /// <summary>
    /// The room an online player is in, from their presence, named from its row; null when they
    /// are in none. Only asked of an online player, whose presence is already running.
    /// </summary>
    private async Task<PlayerRoomRef?> CurrentRoomAsync(
        TurboDbContext db,
        int playerId,
        CancellationToken ct
    )
    {
        var pointer = await grainFactory
            .GetPlayerPresenceGrain(PlayerId.Parse(playerId))
            .GetActiveRoomAsync(ct)
            .ConfigureAwait(false);

        if (pointer.RoomId.Value <= 0)
            return null;

        var roomId = pointer.RoomId.Value;
        var name = await db
            .Rooms.AsNoTracking()
            .Where(x => x.Id == roomId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return name is null ? null : new PlayerRoomRef(roomId, name);
    }

    private static string? NameOf(Dictionary<int, string> names, int? id) =>
        id is { } known ? names.GetValueOrDefault(known, $"#{known}") : null;
}
