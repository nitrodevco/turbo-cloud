using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Database.Achievements;
using Turbo.Database.Context;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Orleans;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Achievements;

/// <summary>
/// The plugin-facing way to record a fact. The fact is saved first, so it survives anything that
/// happens next; the player's progress is then updated off the caller's turn and at most one
/// update per player runs at a time, with one more queued if facts keep arriving. The recovery
/// poll is the backstop if an update is lost.
/// </summary>
public sealed class AchievementFacts(
    IDbContextFactory<TurboDbContext> database,
    IAchievementFactRecorder recorder,
    IGrainFactory grains,
    IHostApplicationLifetime lifetime,
    ILogger<AchievementFacts> logger
) : IAchievementFacts
{
    private readonly object _gate = new();

    /// <summary>Players with an update running; true when another fact arrived meanwhile.</summary>
    private readonly Dictionary<int, bool> _updating = [];

    public async Task RecordAsync(PlayerId playerId, AchievementFact fact, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var scope = db.ConfigureAwait(false);
        if (
            await db
                .AchievementFacts.AsNoTracking()
                .AnyAsync(
                    x => x.PlayerId == playerId.Value && x.OperationId == fact.OperationId,
                    ct
                )
                .ConfigureAwait(false)
        )
            return;
        recorder.Record(db, playerId, fact);
        if (!db.ChangeTracker.HasChanges())
            return;
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Another caller may have recorded the same operation between the check and the save;
            // that is a success. Anything else is a real failure and is rethrown.
            if (!await AlreadyRecordedAsync(playerId, fact, ct).ConfigureAwait(false))
                throw;

            return;
        }
        RequestUpdate(playerId);
    }

    private async Task<bool> AlreadyRecordedAsync(
        PlayerId playerId,
        AchievementFact fact,
        CancellationToken ct
    )
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var scope = db.ConfigureAwait(false);

        return await db
            .AchievementFacts.AsNoTracking()
            .AnyAsync(x => x.PlayerId == playerId.Value && x.OperationId == fact.OperationId, ct)
            .ConfigureAwait(false);
    }

    private void RequestUpdate(PlayerId playerId)
    {
        lock (_gate)
        {
            if (_updating.ContainsKey(playerId.Value))
            {
                _updating[playerId.Value] = true;

                return;
            }
            _updating[playerId.Value] = false;
        }
        Task.Run(() => UpdateAsync(playerId))
            .LogAndForget(logger, "Achievement update for player {PlayerId}", playerId);
    }

    private async Task UpdateAsync(PlayerId playerId)
    {
        try
        {
            do
            {
                await grains
                    .GetPlayerAchievementGrain(playerId)
                    .ProcessAsync(lifetime.ApplicationStopping)
                    .ConfigureAwait(false);
            } while (TakeQueuedUpdate(playerId));
        }
        catch (Exception ex)
        {
            // The fact is saved, so the recovery poll will still update the player.
            logger.LogError(ex, "Achievement update failed for player {PlayerId}", playerId);
        }
        finally
        {
            lock (_gate)
                _updating.Remove(playerId.Value);
        }
    }

    private bool TakeQueuedUpdate(PlayerId playerId)
    {
        lock (_gate)
        {
            if (!_updating[playerId.Value])
                return false;
            _updating[playerId.Value] = false;

            return true;
        }
    }
}
