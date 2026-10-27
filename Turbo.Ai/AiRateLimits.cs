using System;
using System.Collections.Generic;
using System.Threading;

namespace Turbo.Ai;

/// <summary>
/// Runs started a minute, per plugin and per player (<c>Turbo:Ai:PluginRunsPerMinute</c>,
/// <c>PlayerRunsPerMinute</c>), counted on this silo over the last 60 seconds.
/// </summary>
public sealed class AiRateLimits(TimeProvider clock)
{
    private static readonly TimeSpan WINDOW = TimeSpan.FromMinutes(1);

    private readonly Lock _lock = new();
    private readonly Dictionary<string, Queue<DateTimeOffset>> _runs = [];

    /// <summary>Counts a run for each key, when every key is under its limit (0 for none); false, counting nothing, when one is over.</summary>
    public bool TryStart(params (string Key, int Limit)[] keys)
    {
        var now = clock.GetUtcNow();

        lock (_lock)
        {
            foreach (var (key, limit) in keys)
                if (limit > 0 && Recent(key, now).Count >= limit)
                    return false;

            foreach (var (key, limit) in keys)
                if (limit > 0)
                    Recent(key, now).Enqueue(now);

            return true;
        }
    }

    private Queue<DateTimeOffset> Recent(string key, DateTimeOffset now)
    {
        if (!_runs.TryGetValue(key, out var runs))
            _runs[key] = runs = new Queue<DateTimeOffset>();

        while (runs.Count > 0 && now - runs.Peek() >= WINDOW)
            runs.Dequeue();

        return runs;
    }
}
