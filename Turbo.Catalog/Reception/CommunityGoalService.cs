using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Catalog.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Hotel;
using Turbo.Database.Extensions;
using Turbo.Primitives.Hotel;
using Turbo.Primitives.Hotel.Enums;
using Turbo.Primitives.Hotel.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Catalog.Reception;

/// <summary>
/// The community goals (<see cref="ICommunityGoalService"/>), kept in <c>community_goals</c>,
/// with what each player gave in <c>community_goal_contributions</c>. A contribution is added in
/// the database (<c>score = score + n</c>), so purchases on any silo count once each. The goals
/// and each goal's totals are kept for <see cref="ReceptionConfig.CacheSeconds"/>.
/// </summary>
public sealed partial class CommunityGoalService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<CatalogConfig> config,
    TimeProvider time,
    ILogger<CommunityGoalService> logger
) : ICommunityGoalService
{
    private readonly ReceptionConfig _config = config.Value.Reception;

    private (DateTimeOffset ReadAt, ImmutableArray<CommunityGoalSnapshot> Goals)? _goals;

    private readonly ConcurrentDictionary<int, (DateTimeOffset ReadAt, int One, int Two)> _totals =
        new();

    public async Task<CommunityGoalProgressSnapshot?> GetProgressAsync(
        PlayerId player,
        CancellationToken ct
    )
    {
        var now = time.GetUtcNow();
        var goal = Shown(await GoalsAsync(ct).ConfigureAwait(false), now.UtcDateTime);

        if (goal is null)
            return null;

        var (one, two) = await TotalsAsync(goal.Id, ct).ConfigureAwait(false);
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var mine = await dbCtx
            .CommunityGoalContributions.AsNoTracking()
            .Where(x => x.GoalEntityId == goal.Id && x.PlayerEntityId == player.Value)
            .Select(x => x.Score)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        var rank =
            mine > 0
                ? 1
                    + await dbCtx
                        .CommunityGoalContributions.CountAsync(
                            x => x.GoalEntityId == goal.Id && x.Score > mine,
                            ct
                        )
                        .ConfigureAwait(false)
                : 0;

        return CommunityGoalProgress.Compute(goal, one, two, now.UtcDateTime, mine, rank);
    }

    public async Task<ImmutableArray<CommunityGoalContributorSnapshot>> GetHallOfFameAsync(
        string code,
        CancellationToken ct
    )
    {
        var goal = (await GoalsAsync(ct).ConfigureAwait(false)).FirstOrDefault(x => x.Code == code);

        return goal is null ? [] : await TopAsync(goal.Id, ct).ConfigureAwait(false);
    }

    public async Task<bool> VoteAsync(PlayerId player, int side, CancellationToken ct)
    {
        if (side is not (1 or 2))
            return false;

        var now = time.GetUtcNow().UtcDateTime;
        var goal = Shown(await GoalsAsync(ct).ConfigureAwait(false), now);

        if (goal is not { Mode: CommunityGoalMode.VersusVote } || now >= goal.EndsAt)
            return false;

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var voted = await dbCtx
                .CommunityGoalContributions.Where(x =>
                    x.GoalEntityId == goal.Id
                    && x.PlayerEntityId == player.Value
                    && x.VotedSide == null
                )
                .ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(x => x.VotedSide, side)
                            .SetProperty(x => x.SideOne, x => x.SideOne + (side == 1 ? 1 : 0))
                            .SetProperty(x => x.SideTwo, x => x.SideTwo + (side == 2 ? 1 : 0))
                            .SetProperty(x => x.Score, x => x.Score + 1),
                    ct
                )
                .ConfigureAwait(false);

            if (voted > 0)
                break;

            var has = await dbCtx
                .CommunityGoalContributions.AnyAsync(
                    x => x.GoalEntityId == goal.Id && x.PlayerEntityId == player.Value,
                    ct
                )
                .ConfigureAwait(false);

            // Voted before: a second vote doesn't count.
            if (has)
                return false;

            if (
                await TryAddAsync(
                        dbCtx,
                        new CommunityGoalContributionEntity
                        {
                            GoalEntityId = goal.Id,
                            PlayerEntityId = player.Value,
                            SideOne = side == 1 ? 1 : 0,
                            SideTwo = side == 2 ? 1 : 0,
                            Score = 1,
                            VotedSide = side,
                        },
                        ct
                    )
                    .ConfigureAwait(false)
            )
                break;
        }

        _totals.TryRemove(goal.Id, out _);

        return true;
    }

    public async Task ContributeAsync(
        PlayerId player,
        int catalogPageId,
        int amount,
        CancellationToken ct
    )
    {
        if (amount <= 0)
            return;

        var now = time.GetUtcNow().UtcDateTime;
        var goal = Shown(await GoalsAsync(ct).ConfigureAwait(false), now);

        if (goal is null || now >= goal.EndsAt)
            return;

        var side =
            goal.SideOnePageId == catalogPageId ? 1
            : goal.IsVersus && goal.SideTwoPageId == catalogPageId ? 2
            : 0;

        if (side == 0)
            return;

        var one = side == 1 ? amount : 0;
        var two = side == 2 ? amount : 0;
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var added = await dbCtx
                .CommunityGoalContributions.Where(x =>
                    x.GoalEntityId == goal.Id && x.PlayerEntityId == player.Value
                )
                .ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(x => x.SideOne, x => x.SideOne + one)
                            .SetProperty(x => x.SideTwo, x => x.SideTwo + two)
                            .SetProperty(x => x.Score, x => x.Score + amount),
                    ct
                )
                .ConfigureAwait(false);

            if (added > 0)
                break;

            if (
                await TryAddAsync(
                        dbCtx,
                        new CommunityGoalContributionEntity
                        {
                            GoalEntityId = goal.Id,
                            PlayerEntityId = player.Value,
                            SideOne = one,
                            SideTwo = two,
                            Score = amount,
                        },
                        ct
                    )
                    .ConfigureAwait(false)
            )
                break;
        }

        _totals.TryRemove(goal.Id, out _);
    }

    public Task<ImmutableArray<CommunityGoalSnapshot>> ListAsync(CancellationToken ct) =>
        ReadGoalsAsync(ct);

    public async Task<CommunityGoalSnapshot> SaveAsync(
        CommunityGoalSnapshot goal,
        CancellationToken ct
    )
    {
        var code = goal.Code.Trim();

        if (!CodePattern().IsMatch(code) || code.Length > CommunityGoalEntity.CODE_MAX_LENGTH)
            throw new ArgumentException(
                $"A goal's code is a word of letters, digits and _, {CommunityGoalEntity.CODE_MAX_LENGTH} at most: its texts are found by it.",
                nameof(goal)
            );

        if (!Enum.IsDefined(goal.Mode))
            throw new ArgumentException("That is no kind of goal.", nameof(goal));

        if (goal.EndsAt <= goal.StartsAt)
            throw new ArgumentException("A goal ends after it starts.", nameof(goal));

        if (
            goal.LevelScores.Length is 0 or > CommunityGoalProgress.MAX_LEVELS
            || goal.LevelScores[0] <= 0
            || !Rising(goal.LevelScores)
        )
            throw new ArgumentException(
                $"A goal has one to {CommunityGoalProgress.MAX_LEVELS} levels, each a higher score than the one before.",
                nameof(goal)
            );

        if (goal.RewardRanks.Any(x => x <= 0) || !Rising(goal.RewardRanks))
            throw new ArgumentException(
                "Prize ranks rise: the last rank of each band, the best band first.",
                nameof(goal)
            );

        if (!goal.IsVersus && goal.SideTwoPageId is not null)
            throw new ArgumentException("Only a versus goal has a second side.", nameof(goal));

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        if (
            await dbCtx
                .CommunityGoals.AnyAsync(x => x.Code == code && x.Id != goal.Id, ct)
                .ConfigureAwait(false)
        )
            throw new ArgumentException($"There is a goal {code} already.", nameof(goal));

        CommunityGoalEntity row;

        if (goal.Id == 0)
        {
            row = new CommunityGoalEntity
            {
                Code = code,
                LevelScores = "",
                RewardRanks = "",
            };
            dbCtx.CommunityGoals.Add(row);
        }
        else
        {
            row =
                await dbCtx
                    .CommunityGoals.FirstOrDefaultAsync(x => x.Id == goal.Id, ct)
                    .ConfigureAwait(false)
                ?? throw new ArgumentException(
                    $"There is no community goal {goal.Id}.",
                    nameof(goal)
                );
        }

        row.Code = code;
        row.Mode = goal.Mode;
        row.StartsAt = DateTime.SpecifyKind(goal.StartsAt, DateTimeKind.Utc);
        row.EndsAt = DateTime.SpecifyKind(goal.EndsAt, DateTimeKind.Utc);
        row.LevelScores = string.Join(',', goal.LevelScores);
        row.RewardRanks = string.Join(',', goal.RewardRanks);
        row.SideOnePageId = goal.SideOnePageId;
        row.SideTwoPageId = goal.IsVersus ? goal.SideTwoPageId : null;

        if (row.RewardRanks.Length > CommunityGoalEntity.LIST_MAX_LENGTH)
            throw new ArgumentException("Too many prize bands.", nameof(goal));

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        _goals = null;
        logger.LogInformation("Community goal {GoalId} ({Code}) saved", row.Id, row.Code);

        return row.ToSnapshot();
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await dbCtx
            .CommunityGoals.FirstOrDefaultAsync(x => x.Id == id, ct)
            .ConfigureAwait(false);

        if (row is null)
            return false;

        var contributions = await dbCtx
            .CommunityGoalContributions.Where(x => x.GoalEntityId == id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        dbCtx.CommunityGoalContributions.RemoveRange(contributions);
        dbCtx.CommunityGoals.Remove(row);
        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        _goals = null;
        _totals.TryRemove(id, out _);
        logger.LogInformation("Community goal {GoalId} ({Code}) removed", id, row.Code);

        return true;
    }

    public async Task<CommunityGoalStandingSnapshot?> GetStandingAsync(int id, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        if (!await dbCtx.CommunityGoals.AnyAsync(x => x.Id == id, ct).ConfigureAwait(false))
            return null;

        var rows = dbCtx.CommunityGoalContributions.AsNoTracking().Where(x => x.GoalEntityId == id);

        return new CommunityGoalStandingSnapshot
        {
            GoalId = id,
            SideOne = await rows.SumAsync(x => x.SideOne, ct).ConfigureAwait(false),
            SideTwo = await rows.SumAsync(x => x.SideTwo, ct).ConfigureAwait(false),
            Contributors = await rows.CountAsync(x => x.Score > 0, ct).ConfigureAwait(false),
            VotesOne = await rows.CountAsync(x => x.VotedSide == 1, ct).ConfigureAwait(false),
            VotesTwo = await rows.CountAsync(x => x.VotedSide == 2, ct).ConfigureAwait(false),
            Top = await TopAsync(id, ct).ConfigureAwait(false),
        };
    }

    /// <summary>The goal the reception shows: the last one started, running or over.</summary>
    private static CommunityGoalSnapshot? Shown(
        ImmutableArray<CommunityGoalSnapshot> goals,
        DateTime now
    ) => goals.Where(x => x.StartsAt <= now).MaxBy(x => x.StartsAt);

    private async Task<ImmutableArray<CommunityGoalSnapshot>> GoalsAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var read = _goals;

        if (read is null || now - read.Value.ReadAt >= TimeSpan.FromSeconds(_config.CacheSeconds))
        {
            read = (now, await ReadGoalsAsync(ct).ConfigureAwait(false));
            _goals = read;
        }

        return read.Value.Goals;
    }

    private async Task<ImmutableArray<CommunityGoalSnapshot>> ReadGoalsAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rows = await dbCtx
            .CommunityGoals.AsNoTracking()
            .OrderByDescending(x => x.StartsAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return [.. rows.Select(x => x.ToSnapshot())];
    }

    private async Task<(int One, int Two)> TotalsAsync(int goalId, CancellationToken ct)
    {
        var now = time.GetUtcNow();

        if (
            _totals.TryGetValue(goalId, out var kept)
            && now - kept.ReadAt < TimeSpan.FromSeconds(_config.CacheSeconds)
        )
            return (kept.One, kept.Two);

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rows = dbCtx
            .CommunityGoalContributions.AsNoTracking()
            .Where(x => x.GoalEntityId == goalId);
        var one = await rows.SumAsync(x => x.SideOne, ct).ConfigureAwait(false);
        var two = await rows.SumAsync(x => x.SideTwo, ct).ConfigureAwait(false);

        _totals[goalId] = (now, one, two);

        return (one, two);
    }

    private async Task<ImmutableArray<CommunityGoalContributorSnapshot>> TopAsync(
        int goalId,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var top = await dbCtx
            .CommunityGoalContributions.AsNoTracking()
            .Where(x => x.GoalEntityId == goalId && x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Id)
            .Take(Math.Max(0, _config.HallOfFameSize))
            .Select(x => new
            {
                x.PlayerEntityId,
                x.PlayerEntity!.Name,
                x.PlayerEntity.Figure,
                x.Score,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return
        [
            .. top.Select(
                (x, i) =>
                    new CommunityGoalContributorSnapshot
                    {
                        PlayerId = x.PlayerEntityId,
                        Name = x.Name,
                        Figure = x.Figure,
                        Rank = i + 1,
                        Score = x.Score,
                    }
            ),
        ];
    }

    /// <summary>Adds a player's first row; false when another got there first (try the update again).</summary>
    private async Task<bool> TryAddAsync(
        TurboDbContext dbCtx,
        CommunityGoalContributionEntity row,
        CancellationToken ct
    )
    {
        dbCtx.CommunityGoalContributions.Add(row);

        try
        {
            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

            return true;
        }
        catch (DbUpdateException ex)
        {
            dbCtx.Entry(row).State = EntityState.Detached;
            logger.LogDebug(ex, "A contribution row was added at the same time; adding to it");

            return false;
        }
    }

    private static bool Rising(ImmutableArray<int> values)
    {
        for (var i = 1; i < values.Length; i++)
            if (values[i] <= values[i - 1])
                return false;

        return true;
    }

    [GeneratedRegex("^[A-Za-z0-9_]+$")]
    private static partial Regex CodePattern();
}
