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
using Orleans.Runtime;
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
///
/// Requests answer from memory. Each board held is the score histogram (how many players hold
/// each score, which gives any score its rank) and the first ranked entries; a timer reads
/// them again every <see cref="BadgeConfig.LeaderboardCacheMs"/>. The timer and both methods
/// are interleaved, so a refresh in flight never holds up a request: boards are immutable and
/// swapped in whole between awaits. Only a board asked for the first time, a chunk past the
/// entries held and a player's own score outside them are read on the request path, and those
/// run interleaved too.
/// </summary>
internal sealed class BadgeLeaderboardGrain : Grain, IBadgeLeaderboardGrain
{
    private const int NO_RARITY = -1;

    /// <summary>The board every avatar's rank is read from, so it is always held.</summary>
    private static readonly BadgeLeaderboardKey TOTAL_BADGES = new(
        BadgeLeaderboardType.TotalBadges,
        NO_RARITY
    );

    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly BadgeConfig _badgeConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IBadgeLeaderboardGrain> _logger;

    private readonly BadgeLeaderboardLiveState _state = new();

    private IDisposable? _refreshTimer;

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

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        // Not rethrown: this grain is a cache the timer fills. Until the total badges board is
        // read, a rank is BadgeRanks.NONE, which is what a failed read answered before.
        try
        {
            await ReadBoardAsync(TOTAL_BADGES, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read the total badges board on activation");
        }

        _refreshTimer = this.RegisterGrainTimer<object?>(
            static async (self, ct) => await ((BadgeLeaderboardGrain)self!).RefreshAsync(ct),
            this,
            new GrainTimerCreationOptions
            {
                DueTime = TimeSpan.FromMilliseconds(_badgeConfig.LeaderboardCacheMs),
                Period = TimeSpan.FromMilliseconds(_badgeConfig.LeaderboardCacheMs),
                Interleave = true,
            }
        );
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        _refreshTimer?.Dispose();
        _refreshTimer = null;

        return Task.CompletedTask;
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

        var key = new BadgeLeaderboardKey(type, rarity);

        try
        {
            _state.RequestedBoards.Add(key);

            var board = _state.Boards.GetValueOrDefault(key) ?? await ReadBoardAsync(key, ct);

            if (board is null)
                return EmptyPage(type, rarity, chunkIndex, chunkSize);

            return new BadgeLeaderboardPageSnapshot
            {
                Type = type,
                Rarity = rarity,
                ChunkIndex = chunkIndex,
                ChunkSize = chunkSize,
                TotalEntries = board.TotalEntries,
                Entries = await GetChunkEntriesAsync(board, chunkIndex, chunkSize, ct),
                OwnEntry = await GetOwnEntryAsync(forPlayerId, board, ct),
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

    public Task<int> GetTotalBadgesRankAsync(int totalBadges, CancellationToken ct) =>
        Task.FromResult(
            totalBadges > 0 && _state.Boards.TryGetValue(TOTAL_BADGES, out var board)
                ? board.RankOf(totalBadges)
                : BadgeRanks.NONE
        );

    /// <summary>
    /// The timer's refresh: the total badges board and every board asked for since the last
    /// one. A board nobody asked for is dropped rather than read, and read again when asked.
    /// A board that fails keeps what it had and is tried again next time.
    /// </summary>
    private async Task RefreshAsync(CancellationToken ct)
    {
        var wanted = new HashSet<BadgeLeaderboardKey>(_state.RequestedBoards) { TOTAL_BADGES };

        _state.RequestedBoards.Clear();

        foreach (var key in _state.Boards.Keys.Where(x => !wanted.Contains(x)).ToList())
            _state.Boards.Remove(key);

        foreach (var key in wanted)
        {
            try
            {
                await ReadBoardAsync(key, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to refresh badge leaderboard {Type} rarity {Rarity}; keeping the previous one",
                    key.Type,
                    key.Rarity
                );
            }
        }
    }

    /// <summary>
    /// Reads a board and holds it, or answers null for a board that cannot be built. Everything
    /// is read into locals first and the board is swapped in after the last await.
    /// </summary>
    private async Task<BadgeLeaderboardBoard?> ReadBoardAsync(
        BadgeLeaderboardKey key,
        CancellationToken ct
    )
    {
        var codes = await GetBoardCodesAsync(key.Type, key.Rarity, ct);

        if (codes is { Length: 0 })
            return null;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var histogram = await Scores(dbCtx, codes)
            .GroupBy(x => x.Score)
            .Select(g => new { Score = g.Key, Players = g.Count() })
            .ToListAsync(ct);
        var ranked = histogram
            .OrderByDescending(x => x.Score)
            .Select(x => (x.Score, x.Players))
            .ToImmutableArray();
        var rows = await Scores(dbCtx, codes)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.PlayerId)
            .Take(_badgeConfig.LeaderboardHeldEntries)
            .ToListAsync(ct);
        var partial = new BadgeLeaderboardBoard(codes, ranked, ranked.Sum(x => x.Players), []);
        var board = partial with { Top = await ToEntriesAsync(dbCtx, partial, rows, ct) };

        _state.Boards[key] = board;

        return board;
    }

    /// <summary>A chunk from the entries held, or read from the database past them.</summary>
    private async Task<ImmutableArray<BadgeLeaderboardEntrySnapshot>> GetChunkEntriesAsync(
        BadgeLeaderboardBoard board,
        int chunkIndex,
        int chunkSize,
        CancellationToken ct
    )
    {
        // A long: the chunk index is the client's and may be anything.
        var offset = (long)chunkIndex * chunkSize;

        if (offset >= board.TotalEntries)
            return [];

        if (offset + chunkSize <= board.Top.Length || board.Top.Length >= board.TotalEntries)
            return [.. board.Top.Skip((int)offset).Take(chunkSize)];

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var rows = await Scores(dbCtx, board.Codes)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.PlayerId)
            .Skip((int)offset)
            .Take(chunkSize)
            .ToListAsync(ct);

        return await ToEntriesAsync(dbCtx, board, rows, ct);
    }

    /// <summary>
    /// Score rows as board entries, ranked from the board's histogram so players on the same
    /// score share a rank.
    /// </summary>
    private static async Task<ImmutableArray<BadgeLeaderboardEntrySnapshot>> ToEntriesAsync(
        TurboDbContext dbCtx,
        BadgeLeaderboardBoard board,
        List<PlayerScore> rows,
        CancellationToken ct
    )
    {
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
        var entries = ImmutableArray.CreateBuilder<BadgeLeaderboardEntrySnapshot>(rows.Count);

        foreach (var row in rows)
        {
            if (!players.TryGetValue(row.PlayerId, out var player))
                continue;

            entries.Add(
                new BadgeLeaderboardEntrySnapshot
                {
                    PlayerId = row.PlayerId,
                    Name = player.Name,
                    Figure = player.Figure,
                    Rank = board.RankOf(row.Score),
                    Score = row.Score,
                }
            );
        }

        return entries.ToImmutable();
    }

    /// <summary>
    /// The asking player's own line: from the entries held when they are among them, otherwise
    /// their score is counted (one indexed count for one player) and ranked from the histogram.
    /// </summary>
    private async Task<BadgeLeaderboardEntrySnapshot?> GetOwnEntryAsync(
        PlayerId playerId,
        BadgeLeaderboardBoard board,
        CancellationToken ct
    )
    {
        if (playerId <= 0)
            return null;

        var held = board.Top.FirstOrDefault(x => x.PlayerId == playerId);

        // Every player with a score is held, so one who is not has none.
        if (held is not null || board.Top.Length >= board.TotalEntries)
            return held;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var owned = dbCtx
            .PlayerBadges.AsNoTracking()
            .Where(x => x.PlayerEntityId == playerId.Value);

        if (board.Codes is { } codes)
        {
            var codeList = codes.ToList();

            owned = owned.Where(x => codeList.Contains(x.BadgeCode));
        }

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
            Rank = board.RankOf(score),
            Score = score,
        };
    }

    /// <summary>
    /// The codes a board counts: null for every badge, the codes of a tier for a rarity board,
    /// and none at all for a board that cannot be built. Which codes are of a tier is the
    /// directory's to say.
    /// </summary>
    private async Task<ImmutableArray<string>?> GetBoardCodesAsync(
        BadgeLeaderboardType type,
        int rarity,
        CancellationToken ct
    ) =>
        type switch
        {
            BadgeLeaderboardType.TotalBadges => null,
            BadgeLeaderboardType.Rarity when Enum.IsDefined((BadgeRarityType)rarity) =>
                await _grainFactory
                    .GetBadgeDirectoryGrain()
                    .GetCodesOfRarityAsync((BadgeRarityType)rarity, ct),
            // Achievement levels have no data behind them until achievements exist.
            _ => ImmutableArray<string>.Empty,
        };

    /// <summary>Badges per player, over every badge or only the given codes.</summary>
    private static IQueryable<PlayerScore> Scores(
        TurboDbContext dbCtx,
        ImmutableArray<string>? codes
    )
    {
        var badges = dbCtx.PlayerBadges.AsNoTracking();

        if (codes is { } list)
        {
            // A List, which EF turns into an IN over the codes.
            var codeList = list.ToList();

            badges = badges.Where(x => codeList.Contains(x.BadgeCode));
        }

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
