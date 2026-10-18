using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Achievements.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Quests;
using Turbo.Primitives.Quests.Enums;
using Turbo.Primitives.Quests.RewardTracks;

namespace Turbo.Achievements;

/// <summary>
/// The reward tracks in the database, cached. Writes go through one gate and reload the cache
/// before they return, so what a write saved is what players get next. On the first load, a
/// track still listed under <c>Turbo:RewardTracks:Tracks</c> that the database lacks is moved
/// in, so a hotel that configured its tracks keeps them.
/// </summary>
public sealed class RewardTrackStore(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<RewardTrackConfig> config,
    ILogger<RewardTrackStore> logger
) : IRewardTrackStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ImmutableArray<RewardTrackDefinition> _tracks = [];
    private ImmutableHashSet<string> _counted = [];
    private bool _configMoved;

    public ImmutableArray<RewardTrackDefinition> Tracks => _tracks;

    public ImmutableArray<RewardTrackDefinition> LiveAt(DateTime utcNow) =>
        [.. _tracks.Where(x => x.IsLiveAt(utcNow))];

    public bool Counts(string actionType) => _counted.Contains(actionType);

    public async Task ReloadAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_configMoved)
            {
                await MoveConfiguredTracksAsync(ct).ConfigureAwait(false);
                _configMoved = true;
            }

            await LoadAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(IReadOnlyList<RewardTrackDefinition> tracks, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var saved = tracks.Select(x => x.Id).ToHashSet();

            RewardTrackRules.Check([.. _tracks.Where(x => !saved.Contains(x.Id)), .. tracks]);
            await WriteAsync(tracks, ct).ConfigureAwait(false);
            await LoadAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> DeleteAsync(string trackId, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            var row = await db
                .RewardTracks.FirstOrDefaultAsync(x => x.TrackId == trackId, ct)
                .ConfigureAwait(false);

            if (row is null)
                return false;

            db.RewardTracks.Remove(row);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await LoadAsync(ct).ConfigureAwait(false);

            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task MoveConfiguredTracksAsync(CancellationToken ct)
    {
        var configured = config.Value.Tracks;

        if (configured.Count == 0)
            return;

        await using var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var present = await db
            .RewardTracks.AsNoTracking()
            .Select(x => x.TrackId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var missing = configured.Where(x => !present.Contains(x.Id)).ToList();

        foreach (var track in configured.Where(x => present.Contains(x.Id)))
            logger.LogWarning(
                "Reward track {TrackId} is configured under {Section} and in the database; the database's is used. Remove it from the configuration.",
                track.Id,
                RewardTrackConfig.SECTION_NAME
            );

        if (missing.Count == 0)
            return;

        await WriteAsync(missing, ct).ConfigureAwait(false);

        logger.LogWarning(
            "Moved reward tracks {TrackIds} from {Section} into the database; change them in the panel and remove them from the configuration.",
            string.Join(", ", missing.Select(x => x.Id)),
            RewardTrackConfig.SECTION_NAME
        );
    }

    private async Task WriteAsync(IReadOnlyList<RewardTrackDefinition> tracks, CancellationToken ct)
    {
        await using var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var transaction = await db
            .Database.BeginTransactionAsync(ct)
            .ConfigureAwait(false);
        var ids = tracks.Select(x => x.Id).ToList();
        var existing = await db
            .RewardTracks.Where(x => ids.Contains(x.TrackId))
            .ToDictionaryAsync(x => x.TrackId, ct)
            .ConfigureAwait(false);
        var nextOrder =
            (await db.RewardTracks.MaxAsync(x => (int?)x.SortOrder, ct).ConfigureAwait(false) ?? -1)
            + 1;

        // A track saved again is written whole: its old tasks, levels and prizes go first.
        db.RewardTracks.RemoveRange(existing.Values);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        foreach (var track in tracks)
            db.RewardTracks.Add(
                Row(
                    track,
                    existing.TryGetValue(track.Id, out var before) ? before.SortOrder : nextOrder++
                )
            );

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        await using var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var rows = await db
            .RewardTracks.AsNoTracking()
            .Include(x => x.Tasks)
                .ThenInclude(x => x.Levels)
            .Include(x => x.Prizes)
            .AsSplitQuery()
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var tracks = rows.OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(Definition)
            .ToImmutableArray();

        if (RewardTrackRules.Problem(tracks) is { } problem)
            logger.LogError("The reward tracks in the database are invalid: {Problem}", problem);

        _tracks = tracks;
        // Dates are left to the player's grain, which checks them at the time of each count.
        _counted = tracks
            .Where(x => x.Enabled)
            .SelectMany(x => x.Tasks)
            .Select(x => x.ActionType)
            .ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static RewardTrackDefinition Definition(RewardTrackEntity row) =>
        new()
        {
            Id = row.TrackId,
            Enabled = row.Enabled,
            StartsAt = Utc(row.StartsAt),
            EndsAt = Utc(row.EndsAt),
            Theme = row.Theme,
            Premium = row.HasPremium
                ? new()
                {
                    TaskPointsBoost = row.PremiumTaskPointsBoost,
                    InstantPoints = row.PremiumInstantPoints,
                    CostCredits = row.PremiumCostCredits,
                    CostDiamonds = row.PremiumCostDiamonds,
                }
                : null,
            Tasks =
            [
                .. row
                    .Tasks.OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Id)
                    .Select(task => new RewardTrackTaskDefinition
                    {
                        Id = task.TaskId,
                        ActionType = task.ActionType,
                        Parameter = task.Parameter,
                        Premium = task.Premium,
                        Levels =
                        [
                            .. task
                                .Levels.OrderBy(x => x.RequiredCount)
                                .Select(level => new RewardTrackTaskLevelDefinition
                                {
                                    RequiredCount = level.RequiredCount,
                                    Points = level.Points,
                                    Premium = level.Premium,
                                }),
                        ],
                    }),
            ],
            Prizes =
            [
                .. row
                    .Prizes.OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Id)
                    .Select(prize => new RewardTrackPrizeDefinition
                    {
                        Id = prize.PrizeId,
                        RequiredPoints = prize.RequiredPoints,
                        ProductType = (ProductDisplayType)prize.ProductType,
                        RewardTypeId = prize.RewardTypeId,
                        ExtraParams = prize.ExtraParams,
                        Amount = prize.Amount,
                        Premium = prize.Premium,
                        FurnitureDefinitionId = prize.FurnitureDefinitionId,
                        Name = prize.Name,
                    }),
            ],
        };

    private static RewardTrackEntity Row(RewardTrackDefinition track, int sortOrder) =>
        new()
        {
            TrackId = track.Id,
            Theme = track.Theme,
            Enabled = track.Enabled,
            StartsAt = Utc(track.StartsAt),
            EndsAt = Utc(track.EndsAt),
            SortOrder = sortOrder,
            HasPremium = track.Premium is not null,
            PremiumTaskPointsBoost = track.Premium?.TaskPointsBoost ?? 1,
            PremiumInstantPoints = track.Premium?.InstantPoints ?? 0,
            PremiumCostCredits = track.Premium?.CostCredits ?? 0,
            PremiumCostDiamonds = track.Premium?.CostDiamonds ?? 0,
            Tasks =
            [
                .. track.Tasks.Select(
                    (task, i) =>
                        new RewardTrackTaskEntity
                        {
                            TaskId = task.Id,
                            ActionType = task.ActionType,
                            Parameter = task.Parameter,
                            Premium = task.Premium,
                            SortOrder = i,
                            Levels =
                            [
                                .. task.Levels.Select(level => new RewardTrackTaskLevelEntity
                                {
                                    RequiredCount = level.RequiredCount,
                                    Points = level.Points,
                                    Premium = level.Premium,
                                }),
                            ],
                        }
                ),
            ],
            Prizes =
            [
                .. track.Prizes.Select(
                    (prize, i) =>
                        new RewardTrackPrizeEntity
                        {
                            PrizeId = prize.Id,
                            RequiredPoints = prize.RequiredPoints,
                            ProductType = (short)prize.ProductType,
                            RewardTypeId = prize.RewardTypeId,
                            ExtraParams = prize.ExtraParams,
                            Amount = prize.Amount,
                            Premium = prize.Premium,
                            FurnitureDefinitionId = prize.FurnitureDefinitionId,
                            Name = prize.Name,
                            SortOrder = i,
                        }
                ),
            ],
        };

    private static DateTime? Utc(DateTime? value) =>
        value is { } t
            ? t.Kind switch
            {
                DateTimeKind.Utc => t,
                DateTimeKind.Local => t.ToUniversalTime(),
                _ => DateTime.SpecifyKind(t, DateTimeKind.Utc),
            }
            : null;
}
