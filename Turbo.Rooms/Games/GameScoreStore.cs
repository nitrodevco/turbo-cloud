using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Database.Context;
using Turbo.Database.Entities.Games;
using Turbo.Primitives.Games;
using Turbo.Primitives.Games.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Games;

/// <summary>
/// The game centre's stored results (<c>game_player_results</c>): a row per player per game,
/// written when the game starts and scored when it reports. Games played, the day's games and
/// every leaderboard are read from them; nothing is cached, so a result shows at once.
/// </summary>
public sealed class GameScoreStore(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptionsMonitor<GameCenterConfig> config,
    GameRegistry registry,
    TimeProvider clock,
    ILogger<GameScoreStore> logger
) : IGameResultService, IGameLeaderboardService
{
    /// <summary>Counts a started game for each of its players.</summary>
    public async Task RecordStartAsync(GameLaunch launch, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await using var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        foreach (var player in launch.Players)
            db.GamePlayerResults.Add(
                new GamePlayerResultEntity
                {
                    GameTypeId = launch.Definition.GameTypeId,
                    GameId = launch.GameId,
                    PlayerEntityId = player.PlayerId,
                    TeamId = player.TeamId,
                    Year = ISOWeek.GetYear(now),
                    Week = ISOWeek.GetWeekOfYear(now),
                    CreatedAt = now,
                }
            );

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>The highest game id stored, so ids stay unique across restarts.</summary>
    public async Task<int> GetLastGameIdAsync(CancellationToken ct)
    {
        await using var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        return await db
                .GamePlayerResults.MaxAsync(row => (int?)row.GameId, ct)
                .ConfigureAwait(false)
            ?? 0;
    }

    /// <summary>Games the player started of this type: all of them, and since midnight UTC.</summary>
    public async Task<(int Total, int Today)> CountGamesAsync(
        PlayerId playerId,
        int gameTypeId,
        CancellationToken ct
    )
    {
        var midnight = clock.GetUtcNow().UtcDateTime.Date;
        await using var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var rows = db
            .GamePlayerResults.AsNoTracking()
            .Where(row => row.PlayerEntityId == playerId.Value && row.GameTypeId == gameTypeId);

        var total = await rows.CountAsync(ct).ConfigureAwait(false);
        var today = await rows.CountAsync(row => row.CreatedAt >= midnight, ct)
            .ConfigureAwait(false);

        return (total, today);
    }

    public async Task ReportAsync(GameResult result, CancellationToken ct)
    {
        await using (var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false))
        {
            var playerIds = result.Players.Select(player => player.PlayerId.Value).ToList();
            var rows = await db
                .GamePlayerResults.Where(row =>
                    row.GameTypeId == result.GameTypeId
                    && row.GameId == result.GameId
                    && playerIds.Contains(row.PlayerEntityId)
                )
                .ToListAsync(ct)
                .ConfigureAwait(false);

            foreach (var player in result.Players)
            {
                var row = rows.FirstOrDefault(r => r.PlayerEntityId == player.PlayerId.Value);

                // A player the game never started (a late join of the game's own) still scores.
                if (row is null)
                {
                    var now = clock.GetUtcNow().UtcDateTime;
                    row = new GamePlayerResultEntity
                    {
                        GameTypeId = result.GameTypeId,
                        GameId = result.GameId,
                        PlayerEntityId = player.PlayerId,
                        Year = ISOWeek.GetYear(now),
                        Week = ISOWeek.GetWeekOfYear(now),
                        CreatedAt = now,
                    };
                    db.GamePlayerResults.Add(row);
                }

                row.Score = player.Score;
                row.TeamId = player.TeamId;
                row.Placement = player.Placement;
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        foreach (var handler in registry.RewardHandlers)
        {
            try
            {
                await handler.OnGameResultAsync(result, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Game reward handler {Handler} failed for game {GameId} of type {GameTypeId}",
                    handler.GetType().FullName,
                    result.GameId,
                    result.GameTypeId
                );
            }
        }
    }

    public async Task<GameLeaderboardSnapshot> GetLeaderboardAsync(
        PlayerId playerId,
        GameLeaderboardQuery query,
        CancellationToken ct
    )
    {
        var settings = config.CurrentValue;
        var window = Math.Clamp(query.WindowSize, 1, Math.Max(1, settings.LeaderboardMaxEntries));
        var weekly =
            query.Kind
            is GameLeaderboardKind.Weekly
                or GameLeaderboardKind.WeeklyFriends
                or GameLeaderboardKind.WeeklyGroup;
        var weekOffset = weekly
            ? Math.Clamp(query.WeekOffset, 0, settings.LeaderboardMaxWeeksBack)
            : 0;
        var now = clock.GetUtcNow().UtcDateTime;
        var shown = now.AddDays(-7 * weekOffset);
        var (year, week) = (ISOWeek.GetYear(shown), ISOWeek.GetWeekOfYear(shown));

        await using var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var rows = db
            .GamePlayerResults.AsNoTracking()
            .Where(row => row.GameTypeId == query.GameTypeId);

        if (weekly)
            rows = rows.Where(row => row.Year == year && row.Week == week);

        var favouriteGroupId = 0;
        List<GameLeaderboardEntrySnapshot> ranked;

        if (query.Kind is GameLeaderboardKind.TotalGroup or GameLeaderboardKind.WeeklyGroup)
        {
            favouriteGroupId = await db
                .GuildMembers.AsNoTracking()
                .Where(member => member.PlayerEntityId == playerId.Value && member.IsFavourite)
                .Select(member => member.GuildEntityId)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            // A group scores what its members (those who made it their favourite) scored.
            var groupScores = await (
                from row in rows
                join member in db.GuildMembers on row.PlayerEntityId equals member.PlayerEntityId
                where member.IsFavourite
                group row by member.GuildEntityId into byGroup
                select new { GroupId = byGroup.Key, Score = byGroup.Sum(row => row.Score) }
            )
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var groupIds = groupScores.Select(score => score.GroupId).ToList();
            var groups = await db
                .Guilds.AsNoTracking()
                .Where(guild => groupIds.Contains(guild.Id))
                .ToDictionaryAsync(guild => guild.Id, ct)
                .ConfigureAwait(false);

            ranked = Rank(
                groupScores
                    .Where(score => groups.ContainsKey(score.GroupId))
                    .Select(score =>
                        (
                            score.GroupId,
                            score.Score,
                            groups[score.GroupId].Name,
                            groups[score.GroupId].BadgeCode,
                            ""
                        )
                    )
            );
        }
        else
        {
            if (query.Kind is GameLeaderboardKind.Friends or GameLeaderboardKind.WeeklyFriends)
            {
                var friendIds = await db
                    .MessengerFriends.AsNoTracking()
                    .Where(friend => friend.PlayerEntityId == playerId.Value)
                    .Select(friend => friend.FriendPlayerEntityId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                friendIds.Add(playerId.Value);

                rows = rows.Where(row => friendIds.Contains(row.PlayerEntityId));
            }

            var playerScores = await rows.GroupBy(row => row.PlayerEntityId)
                .Select(byPlayer => new
                {
                    PlayerId = byPlayer.Key,
                    Score = byPlayer.Sum(row => row.Score),
                })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var ids = playerScores.Select(score => score.PlayerId).ToList();
            var players = await db
                .Players.AsNoTracking()
                .Where(player => ids.Contains(player.Id))
                .Select(player => new
                {
                    player.Id,
                    player.Name,
                    player.Figure,
                    player.Gender,
                })
                .ToDictionaryAsync(player => player.Id, ct)
                .ConfigureAwait(false);

            ranked = Rank(
                playerScores
                    .Where(score => players.ContainsKey(score.PlayerId))
                    .Select(score =>
                        (
                            score.PlayerId,
                            score.Score,
                            players[score.PlayerId].Name,
                            players[score.PlayerId].Figure,
                            players[score.PlayerId].Gender.ToLegacyString()
                        )
                    )
            );
        }

        // -1 asks for the page the player (or their group) is on.
        var start = query.StartRank;

        if (start < 1)
        {
            var ownId = query.Kind
                is GameLeaderboardKind.TotalGroup
                    or GameLeaderboardKind.WeeklyGroup
                ? favouriteGroupId
                : playerId.Value;
            var own = ranked.FindIndex(entry => entry.Id == ownId);

            start = own < 0 ? 1 : Math.Max(1, own + 1 - window / 2);
        }

        var nextWeek = ISOWeek
            .ToDateTime(ISOWeek.GetYear(now), ISOWeek.GetWeekOfYear(now), DayOfWeek.Monday)
            .AddDays(7);

        return new GameLeaderboardSnapshot
        {
            Entries = [.. ranked.Skip(start - 1).Take(window)],
            TotalListSize = ranked.Count,
            GameTypeId = query.GameTypeId,
            Year = weekly ? year : 0,
            Week = weekly ? week : 0,
            MaxOffset = weekly ? settings.LeaderboardMaxWeeksBack : 0,
            CurrentOffset = weekOffset,
            MinutesUntilReset = weekly ? (int)Math.Ceiling((nextWeek - now).TotalMinutes) : 0,
            FavouriteGroupId = favouriteGroupId,
        };
    }

    private static List<GameLeaderboardEntrySnapshot> Rank(
        IEnumerable<(int Id, int Score, string Name, string Figure, string Gender)> scores
    ) =>
        scores
            .OrderByDescending(score => score.Score)
            .ThenBy(score => score.Id)
            .Select(
                (score, index) =>
                    new GameLeaderboardEntrySnapshot
                    {
                        Id = score.Id,
                        Score = score.Score,
                        Rank = index + 1,
                        Name = score.Name,
                        Figure = score.Figure,
                        Gender = score.Gender,
                    }
            )
            .ToList();
}
