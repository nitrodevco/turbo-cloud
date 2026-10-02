using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Achievements;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerPresenceGrain
{
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
