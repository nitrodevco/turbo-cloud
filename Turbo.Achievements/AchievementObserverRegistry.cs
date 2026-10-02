using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Orleans;

namespace Turbo.Achievements;

/// <summary>
/// Observers run on the thread pool, one event at a time, so a slow observer or one that calls back
/// into the player's achievement grain can neither stall nor deadlock progression.
/// </summary>
public sealed class AchievementObserverRegistry(ILogger<AchievementObserverRegistry> logger)
    : IAchievementObserverRegistry
{
    private readonly object _gate = new();
    private IAchievementObserver[] _observers = [];

    public IDisposable Register(IEnumerable<IAchievementObserver> observers)
    {
        var batch = observers.ToArray();
        lock (_gate)
        {
            if (
                batch.Length == 0
                || batch.Any(x => x is null || _observers.Contains(x))
                || batch.Distinct().Count() != batch.Length
            )
                throw new ArgumentException(
                    "Invalid or repeated achievement observers.",
                    nameof(observers)
                );
            _observers = [.. _observers, .. batch];
        }
        return new AchievementRegistration(() =>
        {
            lock (_gate)
                _observers = [.. _observers.Except(batch)];
        });
    }

    public void NotifyLevelCompleted(AchievementLevelCompleted completed)
    {
        var observers = Volatile.Read(ref _observers);
        if (observers.Length == 0)
            return;
        Task.Run(() => DispatchAsync(observers, completed))
            .LogAndForget(
                logger,
                "Achievement observers for {AchievementId} level {Level}",
                completed.AchievementId,
                completed.Level
            );
    }

    private async Task DispatchAsync(
        IAchievementObserver[] observers,
        AchievementLevelCompleted completed
    )
    {
        foreach (var observer in observers)
        {
            try
            {
                await observer
                    .OnLevelCompletedAsync(completed, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Achievement observer {Observer} failed for player {PlayerId} achievement {AchievementId} level {Level}",
                    observer.GetType().Name,
                    completed.PlayerId,
                    completed.AchievementId,
                    completed.Level
                );
            }
        }
    }
}
