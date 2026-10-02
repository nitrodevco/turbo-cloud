using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Turbo.Primitives.Achievements;

namespace Turbo.Achievements;

public sealed class AchievementRewardRegistry : IAchievementRewardRegistry
{
    private readonly object _gate = new();
    private ImmutableDictionary<(string, int), IAchievementRewardHandler> _handlers =
        ImmutableDictionary<(string, int), IAchievementRewardHandler>.Empty;

    public IDisposable Register(IEnumerable<IAchievementRewardHandler> handlers)
    {
        var batch = handlers.ToArray();
        lock (_gate)
        {
            if (
                batch.Any(x =>
                    string.IsNullOrWhiteSpace(x.Key)
                    || x.Key == "wallet"
                    || x.Version <= 0
                    || _handlers.ContainsKey((x.Key, x.Version))
                )
                || batch.Select(x => (x.Key, x.Version)).Distinct().Count() != batch.Length
            )
                throw new ArgumentException(
                    "Invalid or colliding reward handlers.",
                    nameof(handlers)
                );
            _handlers = _handlers.AddRange(
                batch.Select(x => new KeyValuePair<(string, int), IAchievementRewardHandler>(
                    (x.Key, x.Version),
                    x
                ))
            );
        }
        return new AchievementRegistration(() =>
        {
            lock (_gate)
                _handlers = _handlers.RemoveRange(batch.Select(x => (x.Key, x.Version)));
        });
    }

    public bool TryGet(string key, int version, out IAchievementRewardHandler handler)
    {
        lock (_gate)
            return _handlers.TryGetValue((key, version), out handler!);
    }
}
