using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Authentication;

namespace Turbo.Authentication;

/// <summary>
/// The registered <see cref="IPlayPolicy"/>s (<see cref="IPlayPolicies"/>), each with the plugin
/// that registered it, run in order (<see cref="IPlayPolicy.Order"/>, then plugin key, then
/// registration); the first refusal is the answer, and none allows.
/// </summary>
internal sealed class PlayPolicies(ILogger<PlayPolicies> logger) : IPlayPolicies
{
    private readonly Lock _gate = new();
    private ImmutableArray<Entry> _entries = [];
    private long _sequence;

    public async ValueTask<PlayDecision> CheckAsync(PlayRequest request, CancellationToken ct)
    {
        foreach (var entry in _entries)
        {
            PlayDecision decision;

            try
            {
                decision = await entry.Policy.CheckAsync(request, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(
                    ex,
                    "The play policy {Policy} of {Owner} failed for player {PlayerId}; it is passed over",
                    entry.Policy.GetType().Name,
                    entry.Owner,
                    request.PlayerId
                );

                continue;
            }

            if (!decision.Allowed)
            {
                logger.LogInformation(
                    "The play policy {Policy} of {Owner} refused player {PlayerId} ({Source}): {Reason}",
                    entry.Policy.GetType().Name,
                    entry.Owner,
                    request.PlayerId,
                    request.Source,
                    decision.Reason
                );

                return decision;
            }
        }

        return PlayDecision.Allow;
    }

    /// <summary>Adds <paramref name="owner"/>'s policies; disposing the result takes them away.</summary>
    public IDisposable Register(string owner, params IPlayPolicy[] policies)
    {
        Entry[] added;

        lock (_gate)
        {
            added = [.. policies.Select(x => new Entry(owner, x, ++_sequence))];
            _entries =
            [
                .. _entries
                    .Concat(added)
                    .OrderBy(x => x.Policy.Order)
                    .ThenBy(x => x.Owner, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.Sequence),
            ];
        }

        return new Removal(this, added);
    }

    private sealed record Entry(string Owner, IPlayPolicy Policy, long Sequence);

    private sealed class Removal(PlayPolicies owner, Entry[] entries) : IDisposable
    {
        public void Dispose()
        {
            lock (owner._gate)
                owner._entries = [.. owner._entries.Where(x => !entries.Contains(x))];
        }
    }
}
