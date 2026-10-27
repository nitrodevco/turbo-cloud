using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;

namespace Turbo.Ai;

/// <summary>
/// The limits counted for the whole cluster: one <see cref="IAiRateLimitGrain"/> per key, so a
/// player's or a plugin's runs a minute hold however many servers they are spread over. Each key
/// is asked in turn; one over its limit takes back what the keys before it counted.
/// </summary>
public sealed class ClusterAiRateLimits(IGrainFactory grains) : IAiRateLimits
{
    public async Task<bool> TryStartAsync(
        CancellationToken ct,
        params (string Key, int Limit)[] keys
    )
    {
        var counted = new List<IAiRateLimitGrain>(keys.Length);
        var reservation = Guid.NewGuid();
        var accepted = false;
        try
        {
            foreach (var (key, limit) in keys)
            {
                ct.ThrowIfCancellationRequested();
                if (limit <= 0)
                    continue;

                var grain = grains.GetGrain<IAiRateLimitGrain>(key);
                // Include the attempted key: a failed RPC may have counted before losing its reply.
                counted.Add(grain);
                if (!await grain.TryStartAsync(reservation, limit, ct).ConfigureAwait(false))
                    return false;
            }
            accepted = true;
            return true;
        }
        finally
        {
            if (!accepted)
                await Task.WhenAll(
                        counted.Select(grain =>
                            grain.UndoAsync(reservation, CancellationToken.None)
                        )
                    )
                    .ConfigureAwait(false);
        }
    }
}
