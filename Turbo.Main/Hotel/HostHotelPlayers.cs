using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Contracts.Hotel;
using Turbo.Database.Context;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Primitives.Rooms;

namespace Turbo.Main.Hotel;

/// <summary>
/// <see cref="IHotelPlayers"/> over the players' table (read only, never tracked), the session
/// gateway and the player grains.
/// </summary>
internal sealed class HostHotelPlayers(
    IDbContextFactory<TurboDbContext> database,
    IGrainFactory grains,
    ISessionGateway sessions,
    ICurrencyTypeProvider currencies
) : IHotelPlayers
{
    public const int MAX_SEARCH = 50;
    public const int MAX_TOP = 100;

    public async Task<HotelPlayer?> GetAsync(PlayerId player, CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        return await Project(db.Players.AsNoTracking().Where(x => x.Id == player.Value))
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<HotelPlayer?> FindAsync(string name, CancellationToken ct)
    {
        var key = PlayerNames.Key(name ?? string.Empty);

        if (key.Length == 0)
            return null;

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        return await Project(db.Players.AsNoTracking().Where(x => x.NameKey == key))
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<PlayerId, string>> NamesAsync(
        IReadOnlyCollection<PlayerId> players,
        CancellationToken ct
    )
    {
        if (players.Count == 0)
            return new Dictionary<PlayerId, string>();

        var ids = players.Select(x => x.Value).Distinct().ToList();

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var names = await db
            .Players.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return names.ToDictionary(x => new PlayerId(x.Id), x => x.Name);
    }

    public async Task<IReadOnlyList<HotelPlayer>> SearchAsync(
        string prefix,
        int take,
        CancellationToken ct
    )
    {
        var key = PlayerNames.Key(prefix ?? string.Empty);
        take = Math.Clamp(take, 0, MAX_SEARCH);

        if (key.Length == 0 || take == 0)
            return [];

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        return await Project(
                db.Players.AsNoTracking()
                    .Where(x => x.NameKey.StartsWith(key))
                    .OrderBy(x => x.NameKey)
                    .ThenBy(x => x.Id)
                    .Take(take)
            )
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public IReadOnlyCollection<PlayerId> OnlineIds() => sessions.GetOnlinePlayerIds();

    public async Task<RoomId?> CurrentRoomAsync(PlayerId player, CancellationToken ct)
    {
        var room = (
            await grains.GetPlayerPresenceGrain(player).GetActiveRoomAsync(ct).ConfigureAwait(false)
        ).RoomId;

        return room.Value > 0 ? room : null;
    }

    public async Task<HotelClub> ClubAsync(PlayerId player, CancellationToken ct)
    {
        var club = await grains
            .GetPlayerSubscriptionGrain(player)
            .GetAsync(SubscriptionType.HabboClub, ct)
            .ConfigureAwait(false);

        return new(club.IsActive, club.CurrentStreakDays);
    }

    public Task<bool> HasPermissionAsync(PlayerId player, string node, CancellationToken ct) =>
        grains.HasPermissionAsync(player, node, ct);

    public async Task<IReadOnlyList<HotelRanked>> TopByAsync(
        PlayerStat stat,
        int take,
        int skip,
        CancellationToken ct
    )
    {
        take = Math.Clamp(take, 0, MAX_TOP);
        skip = Math.Max(0, skip);

        if (take == 0)
            return [];

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var figures = Figures(db, stat);

        if (figures is null)
            return [];

        var rows = await figures
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.PlayerId)
            .Skip(skip)
            .Take(take)
            .Join(
                db.Players.AsNoTracking(),
                x => x.PlayerId,
                p => p.Id,
                (x, p) =>
                    new
                    {
                        x.PlayerId,
                        p.Name,
                        x.Value,
                    }
            )
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return
        [
            .. rows.OrderByDescending(x => x.Value)
                .ThenBy(x => x.PlayerId)
                .Select((x, i) => new HotelRanked(skip + i + 1, x.PlayerId, x.Name, x.Value)),
        ];
    }

    public Task<bool> SendAsync(PlayerId player, IPluginMessage message, CancellationToken ct) =>
        grains.TrySendComposerToPlayerAsync(player, PluginMessageComposer.For(message), ct);

    public Task<bool> SendAsync(PlayerId player, IComposer composer, CancellationToken ct) =>
        grains.TrySendComposerToPlayerAsync(player, composer, ct);

    /// <summary>Each ranked player's figure; null for a currency the hotel has not.</summary>
    private IQueryable<Figure>? Figures(TurboDbContext db, PlayerStat stat)
    {
        var players = db.Players.AsNoTracking().Where(x => !x.IsSystemAccount);

        if (stat == PlayerStat.Respects)
            return players.Select(x => new Figure { PlayerId = x.Id, Value = x.RespectPoints });

        if (stat == PlayerStat.AchievementScore)
            return db
                .AchievementProgress.AsNoTracking()
                .Where(x => players.Any(p => p.Id == x.PlayerId))
                .GroupBy(x => x.PlayerId)
                .Select(x => new Figure
                {
                    PlayerId = x.Key,
                    Value = x.Sum(a => (long)a.ScoreEarned),
                });

        if (
            stat.Currency is not { } name
            || !currencies.TryGetCurrencyKindByName(name, out var kind)
            || !currencies.TryGetCurrencyTypeId(kind, out var typeId)
        )
            return null;

        return db
            .PlayerCurrencies.AsNoTracking()
            .Where(x =>
                x.CurrencyTypeEntityId == typeId && players.Any(p => p.Id == x.PlayerEntityId)
            )
            .Select(x => new Figure { PlayerId = x.PlayerEntityId, Value = x.Amount });
    }

    private static IQueryable<HotelPlayer> Project(IQueryable<PlayerEntity> players) =>
        players.Select(x => new HotelPlayer(
            new PlayerId(x.Id),
            x.Name,
            x.Figure,
            x.Gender,
            x.Motto ?? string.Empty,
            x.CreatedAt,
            x.LastLoginAt
        ));

    private sealed class Figure
    {
        public int PlayerId { get; init; }

        public long Value { get; init; }
    }
}
