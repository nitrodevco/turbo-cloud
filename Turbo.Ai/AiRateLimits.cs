using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Ai;

/// <summary>The limits counted in this process alone: for the test kit and a host without a cluster.</summary>
public sealed class AiRateLimits(TimeProvider clock) : IAiRateLimits
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, RunWindow> _runs = [];

    public Task<bool> TryStartAsync(CancellationToken ct, params (string Key, int Limit)[] keys)
    {
        ct.ThrowIfCancellationRequested();
        var now = clock.GetUtcNow();

        lock (_lock)
        {
            foreach (var (key, limit) in keys)
                if (limit > 0 && !Window(key).Allows(limit, now))
                    return Task.FromResult(false);

            foreach (var (key, limit) in keys)
                if (limit > 0)
                    Window(key).Count(now);

            return Task.FromResult(true);
        }
    }

    private RunWindow Window(string key)
    {
        if (!_runs.TryGetValue(key, out var runs))
            _runs[key] = runs = new RunWindow();

        return runs;
    }
}
