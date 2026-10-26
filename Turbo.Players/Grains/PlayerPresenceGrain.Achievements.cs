using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerPresenceGrain
{
    public Task<PlayerOnlineClockSnapshot> GetOnlineClockAsync(CancellationToken ct) =>
        Task.FromResult(
            new PlayerOnlineClockSnapshot
            {
                SessionStartedUtc = _sessionObserver is null ? null : _state.SessionStartedUtc,
                UnsavedSinceUtc = _sessionObserver is null
                    ? null
                    : _state.AchievementOnlineSinceUtc,
            }
        );

    private async Task PersistOnlineIntervalAsync(CancellationToken ct)
    {
        if (_state.AchievementOnlineSinceUtc is not { } start || _sessionObserver is null)
            return;
        var end = DateTime.UtcNow;
        var session = _state.SessionKey.ToString();
        var generation = _state.SessionGeneration;
        try
        {
            await using var db = await _achievementDatabase.CreateDbContextAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // The player's time online in all, with the interval the achievement counts.
            var elapsedMs = (long)(end - start).TotalMilliseconds;
            if (elapsedMs > 0)
                await db
                    .Players.Where(x => x.Id == PlayerId.Value)
                    .ExecuteUpdateAsync(
                        x =>
                            x.SetProperty(
                                p => p.OnlineMilliseconds,
                                p => p.OnlineMilliseconds + elapsedMs
                            ),
                        ct
                    );
            _achievementFacts.Record(
                db,
                PlayerId,
                new AchievementFact
                {
                    Source = AchievementSources.ONLINE,
                    OperationId = $"online:{session}:{start.Ticks}:{end.Ticks}",
                    SessionId = session,
                    IntervalStartUtc = start,
                    IntervalEndUtc = end,
                    OccurredAtUtc = end,
                }
            );
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            if (_state.SessionGeneration == generation && _state.AchievementOnlineSinceUtc == start)
                _state.AchievementOnlineSinceUtc = end;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to persist online achievement interval for player {PlayerId}",
                PlayerId
            );
        }
    }
}
