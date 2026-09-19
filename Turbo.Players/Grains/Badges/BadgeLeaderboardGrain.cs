using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Players.Configuration;
using Turbo.Primitives.Badges;
using Turbo.Primitives.Badges.Enums;
using Turbo.Primitives.Badges.Grains;
using Turbo.Primitives.Badges.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Players.Grains.Badges;

/// <summary>
/// The badge leaderboards, one grain for the hotel. A board is a grouped query over every
/// player_badges row, which is why it is not part of <see cref="BadgeDirectoryGrain"/>: every
/// badge shown anywhere waits on the directory, and must not wait behind a board being read.
/// It is a read-through cache: nothing here is written back, so there is nothing to flush on
/// deactivation, and an idle grain may go; the next request reads the board again.
/// </summary>
internal sealed class BadgeLeaderboardGrain : Grain, IBadgeLeaderboardGrain
{
    private const int NO_RARITY = -1;

    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly BadgeConfig _badgeConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IBadgeLeaderboardGrain> _logger;

    private readonly BadgeLeaderboardLiveState _state = new();

    public BadgeLeaderboardGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<BadgeConfig> badgeConfig,
        IGrainFactory grainFactory,
        ILogger<IBadgeLeaderboardGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _badgeConfig = badgeConfig.Value;
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public async Task<BadgeLeaderboardPageSnapshot> GetLeaderboardAsync(
        BadgeLeaderboardType type,
        int rarity,
        int chunkIndex,
        int chunkSize,
        PlayerId forPlayerId,
        CancellationToken ct
    )
    {
        // Every number here came from a client; the board is read with what is left after this.
        chunkIndex = Math.Max(0, chunkIndex);
        chunkSize = Math.Clamp(chunkSize, 1, _badgeConfig.LeaderboardMaxChunkSize);

        if (type != BadgeLeaderboardType.Rarity)
            rarity = NO_RARITY;

        try
        {
            var codes = await GetBoardCodesAsync(type, rarity, ct);

            if (codes is { Count: 0 })
                return EmptyPage(type, rarity, chunkIndex, chunkSize);

            var chunk = await GetChunkAsync(
                new BadgeLeaderboardChunkKey(type, rarity, chunkIndex, chunkSize),
                codes,
                ct
            );

            return new BadgeLeaderboardPageSnapshot
            {
                Type = type,
                Rarity = rarity,
                ChunkIndex = chunkIndex,
                ChunkSize = chunkSize,
                TotalEntries = chunk.TotalEntries,
                Entries = chunk.Entries,
                OwnEntry = await GetOwnEntryAsync(forPlayerId, codes, ct),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to read badge leaderboard {Type} rarity {Rarity} chunk {ChunkIndex}",
                type,
                rarity,
                chunkIndex
            );

            return EmptyPage(type, rarity, chunkIndex, chunkSize);
        }
    }

    public async Task<int> GetTotalBadgesRankAsync(int totalBadges, CancellationToken ct)
    {
        if (totalBadges <= 0)
            return BadgeRanks.NONE;

        try
        {
            await EnsureTotalBadgesScoresAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read how many players hold each badge total");

            // Scores read earlier still give a fair rank; with none ever read there is no rank.
            if (_state.TotalBadgesScoresExpireAtMs == 0)
                return BadgeRanks.NONE;
        }

        // Players on the same score share a rank, as on the board itself.
        return _state.TotalBadgesScores.Where(x => x.Score > totalBadges).Sum(x => x.Players) + 1;
    }

    /// <summary>
    /// The codes a board counts: null for every badge, the codes of a tier for a rarity board,
    /// and none at all for a board that cannot be built. Which codes are of a tier is the
    /// directory's to say.
    /// </summary>
    private async Task<List<string>?> GetBoardCodesAsync(
        BadgeLeaderboardType type,
        int rarity,
        CancellationToken ct
    ) =>
        type switch
        {
            BadgeLeaderboardType.TotalBadges => null,
            BadgeLeaderboardType.Rarity when Enum.IsDefined((BadgeRarityType)rarity) =>
            [
                .. await _grainFactory
                    .GetBadgeDirectoryGrain()
                    .GetCodesOfRarityAsync((BadgeRarityType)rarity, ct),
            ],
            // Achievement levels have no data behind them until achievements exist.
            _ => [],
        };

    private async Task<BadgeLeaderboardChunk> GetChunkAsync(
        BadgeLeaderboardChunkKey key,
        List<string>? codes,
        CancellationToken ct
    )
    {
        var now = Environment.TickCount64;

        if (_state.LeaderboardChunks.TryGetValue(key, out var cached) && cached.ExpiresAtMs > now)
            return cached;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var scores = Scores(dbCtx, codes);
        var offset = key.ChunkIndex * key.ChunkSize;
        var total = await scores.CountAsync(ct);
        var rows = await scores
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.PlayerId)
            .Skip(offset)
            .Take(key.ChunkSize)
            .ToListAsync(ct);

        var playerIds = rows.Select(x => x.PlayerId).ToList();
        var players = await dbCtx
            .Players.AsNoTracking()
            .Where(x => playerIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Figure,
            })
            .ToDictionaryAsync(x => x.Id, ct);

        // Players on the same score share a rank. Everyone ahead of the chunk's first row has a
        // higher or equal score, so only that first rank needs counting; a later score change
        // inside the chunk starts at its own position.
        var firstRank =
            rows.Count == 0 ? 0 : await CountPlayersAboveAsync(dbCtx, codes, rows[0].Score, ct) + 1;
        var entries = ImmutableArray.CreateBuilder<BadgeLeaderboardEntrySnapshot>(rows.Count);
        var rank = firstRank;

        for (var i = 0; i < rows.Count; i++)
        {
            if (i > 0 && rows[i].Score != rows[i - 1].Score)
                rank = offset + i + 1;

            if (!players.TryGetValue(rows[i].PlayerId, out var player))
                continue;

            entries.Add(
                new BadgeLeaderboardEntrySnapshot
                {
                    PlayerId = rows[i].PlayerId,
                    Name = player.Name,
                    Figure = player.Figure,
                    Rank = rank,
                    Score = rows[i].Score,
                }
            );
        }

        var chunk = new BadgeLeaderboardChunk(
            total,
            entries.ToImmutable(),
            now + _badgeConfig.LeaderboardCacheMs
        );

        if (_state.LeaderboardChunks.Count >= _badgeConfig.LeaderboardMaxCachedChunks)
            _state.LeaderboardChunks.Clear();

        _state.LeaderboardChunks[key] = chunk;

        return chunk;
    }

    private async Task<BadgeLeaderboardEntrySnapshot?> GetOwnEntryAsync(
        PlayerId playerId,
        List<string>? codes,
        CancellationToken ct
    )
    {
        if (playerId <= 0)
            return null;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var owned = dbCtx
            .PlayerBadges.AsNoTracking()
            .Where(x => x.PlayerEntityId == playerId.Value);

        if (codes is not null)
            owned = owned.Where(x => codes.Contains(x.BadgeCode));

        var score = await owned.CountAsync(ct);

        if (score == 0)
            return null;

        var player = await dbCtx
            .Players.AsNoTracking()
            .Where(x => x.Id == playerId.Value)
            .Select(x => new { x.Name, x.Figure })
            .FirstOrDefaultAsync(ct);

        if (player is null)
            return null;

        return new BadgeLeaderboardEntrySnapshot
        {
            PlayerId = playerId,
            Name = player.Name,
            Figure = player.Figure,
            Rank = await CountPlayersAboveAsync(dbCtx, codes, score, ct) + 1,
            Score = score,
        };
    }

    /// <summary>
    /// How many players hold each badge total, read again once the cache window has passed. It
    /// has one row per distinct total, so it stays small however many players there are, and it
    /// is what lets a rank be answered for every avatar in every room without a query each.
    /// </summary>
    private async Task EnsureTotalBadgesScoresAsync(CancellationToken ct)
    {
        var now = Environment.TickCount64;

        if (_state.TotalBadgesScoresExpireAtMs > now)
            return;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var scores = await Scores(dbCtx, null)
            .GroupBy(x => x.Score)
            .Select(g => new { Score = g.Key, Players = g.Count() })
            .ToListAsync(ct);

        _state.TotalBadgesScores = [.. scores.Select(x => (x.Score, x.Players))];
        _state.TotalBadgesScoresExpireAtMs = now + _badgeConfig.LeaderboardCacheMs;
    }

    private static Task<int> CountPlayersAboveAsync(
        TurboDbContext dbCtx,
        List<string>? codes,
        int score,
        CancellationToken ct
    ) => Scores(dbCtx, codes).CountAsync(x => x.Score > score, ct);

    /// <summary>Badges per player, over every badge or only the given codes.</summary>
    private static IQueryable<PlayerScore> Scores(TurboDbContext dbCtx, List<string>? codes)
    {
        var badges = dbCtx.PlayerBadges.AsNoTracking();

        if (codes is not null)
            badges = badges.Where(x => codes.Contains(x.BadgeCode));

        return badges
            .GroupBy(x => x.PlayerEntityId)
            .Select(g => new PlayerScore { PlayerId = g.Key, Score = g.Count() });
    }

    private static BadgeLeaderboardPageSnapshot EmptyPage(
        BadgeLeaderboardType type,
        int rarity,
        int chunkIndex,
        int chunkSize
    ) =>
        new()
        {
            Type = type,
            Rarity = rarity,
            ChunkIndex = chunkIndex,
            ChunkSize = chunkSize,
            TotalEntries = 0,
            Entries = [],
            OwnEntry = null,
        };

    /// <summary>A row of the grouped query; a named type so it can be composed across methods.</summary>
    private sealed class PlayerScore
    {
        public int PlayerId { get; init; }
        public int Score { get; init; }
    }
}
