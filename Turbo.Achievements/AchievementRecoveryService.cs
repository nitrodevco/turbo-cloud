using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Achievements.Configuration;
using Turbo.Database.Achievements;
using Turbo.Database.Context;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Orleans;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Grains.Respect;

namespace Turbo.Achievements;

internal sealed class AchievementRecoveryService(
    IDbContextFactory<TurboDbContext> database,
    IOptions<AchievementConfig> config,
    IHostApplicationLifetime lifetime,
    IAchievementCatalog catalog,
    IAchievementFactRecorder recorder,
    IGrainFactory grains,
    ILogger<AchievementRecoveryService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var started = lifetime.ApplicationStarted.Register(() => ready.TrySetResult());
        try
        {
            await ready.Task.WaitAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        var cursor = 0;
        var humanOffset = 0;
        var petOffset = 0;
        var rankedCatalog = default(ImmutableArray<AchievementDefinition>);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(config.Value.RecoverySeconds));
        do
        {
            try
            {
                var db = await database.CreateDbContextAsync(stoppingToken).ConfigureAwait(false);
                await using var scope = db.ConfigureAwait(false);
                var current = catalog.Current;
                if (!current.Equals(rankedCatalog))
                {
                    if (
                        current.Any(x =>
                            x.Enabled && !x.Archived && x.Source == AchievementSources.ROOM_RANK
                        )
                    )
                    {
                        await AchievementRoomCriteria
                            .RecordRankingsAsync(db, recorder, stoppingToken)
                            .ConfigureAwait(false);
                        await db.SaveChangesAsync(stoppingToken).ConfigureAwait(false);
                    }
                    rankedCatalog = current;
                }
                var humanPending = db
                    .HumanRespectOperations.AsNoTracking()
                    .Where(x => !x.Completed && !x.Rejected)
                    .OrderBy(x => x.OperationId);
                var operations = await humanPending
                    .Skip(humanOffset)
                    .Take(config.Value.RecoveryBatchSize)
                    .ToListAsync(stoppingToken)
                    .ConfigureAwait(false);
                if (operations.Count == 0)
                {
                    humanOffset = 0;
                    operations = await humanPending
                        .Take(config.Value.RecoveryBatchSize)
                        .ToListAsync(stoppingToken)
                        .ConfigureAwait(false);
                }
                humanOffset += operations.Count;
                foreach (var operation in operations)
                {
                    try
                    {
                        await grains
                            .GetHumanRespectOperationGrain(operation.OperationId)
                            .ExecuteAsync(operation.ActorId, operation.TargetId, stoppingToken)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(
                            ex,
                            "Respect recovery failed for operation {OperationId}",
                            operation.OperationId
                        );
                    }
                }
                var petPending = db
                    .PetRespectOperations.AsNoTracking()
                    .Where(x => !x.Completed && !x.Rejected)
                    .OrderBy(x => x.OperationId);
                var petOperations = await petPending
                    .Skip(petOffset)
                    .Take(config.Value.RecoveryBatchSize)
                    .ToListAsync(stoppingToken)
                    .ConfigureAwait(false);
                if (petOperations.Count == 0)
                {
                    petOffset = 0;
                    petOperations = await petPending
                        .Take(config.Value.RecoveryBatchSize)
                        .ToListAsync(stoppingToken)
                        .ConfigureAwait(false);
                }
                petOffset += petOperations.Count;
                foreach (var operation in petOperations)
                {
                    try
                    {
                        await grains
                            .GetRoomPersistenceGrain(operation.RoomId)
                            .ApplyPetRespectOperationAsync(
                                operation.OperationId,
                                operation.ActorId,
                                operation.PetId,
                                operation.OwnerId,
                                operation.BaseRespect,
                                stoppingToken
                            )
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(
                            ex,
                            "Pet respect recovery failed for operation {OperationId}",
                            operation.OperationId
                        );
                    }
                }
                var pending = db
                    .AchievementFacts.Where(x => !x.Processed)
                    .Select(x => x.PlayerId)
                    .Union(
                        // Presentation is not polled: an offline player is presented their
                        // completed awards when the list is requested on their next session.
                        db.AchievementAwards.Where(x => !x.Completed).Select(x => x.PlayerId)
                    )
                    .Union(
                        db.AchievementProjections.Where(x => x.PublicationPending)
                            .Select(x => x.PlayerId)
                    )
                    .Distinct();
                var players = await pending
                    .Where(x => x > cursor)
                    .OrderBy(x => x)
                    .Take(config.Value.RecoveryBatchSize)
                    .ToListAsync(stoppingToken)
                    .ConfigureAwait(false);
                if (players.Count == 0)
                {
                    cursor = 0;
                    players = await pending
                        .OrderBy(x => x)
                        .Take(config.Value.RecoveryBatchSize)
                        .ToListAsync(stoppingToken)
                        .ConfigureAwait(false);
                }
                await PruneFactsAsync(db, stoppingToken).ConfigureAwait(false);
                foreach (var playerId in players)
                {
                    cursor = playerId;
                    try
                    {
                        await grains
                            .GetPlayerAchievementGrain(playerId)
                            .ProcessAsync(stoppingToken)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(
                            ex,
                            "Achievement recovery failed for player {PlayerId}",
                            playerId
                        );
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Achievement recovery pass failed");
            }
        } while (await WaitForTickAsync(timer, stoppingToken).ConfigureAwait(false));
    }

    private DateTime _nextPruneUtc = DateTime.MinValue;

    /// <summary>
    /// Processed facts only serve idempotent admission. Delete them in bounded batches once
    /// they age past the retention window; progress and awards do not depend on them.
    /// </summary>
    private async Task PruneFactsAsync(TurboDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (config.Value.FactRetentionDays <= 0 || now < _nextPruneUtc)
            return;
        var cutoff = now.AddDays(-config.Value.FactRetentionDays);
        var batch = config.Value.RecoveryBatchSize * 10;
        // MySQL rejects LIMIT inside an IN subquery, so select the ids first.
        var ids = await db
            .AchievementFacts.AsNoTracking()
            .Where(x => x.Processed && x.OccurredAtUtc < cutoff)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .Take(batch)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (ids.Count > 0)
            await db
                .AchievementFacts.Where(x => ids.Contains(x.Id))
                .ExecuteDeleteAsync(ct)
                .ConfigureAwait(false);
        // A full batch means more remain: keep pruning on the next pass; otherwise wait an hour.
        _nextPruneUtc = ids.Count < batch ? now.AddHours(1) : now;
    }

    private static async Task<bool> WaitForTickAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
    }
}
