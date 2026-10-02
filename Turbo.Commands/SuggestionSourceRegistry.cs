using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Turbo.Primitives.Commands;

namespace Turbo.Commands;

/// <summary>
/// The suggestion sources that are loaded: core's, and those of every plugin that is. A name
/// claimed twice fails the second registration with nothing added, as a command name does.
/// </summary>
public sealed class SuggestionSourceRegistry
{
    private readonly Lock _gate = new();
    private readonly List<ISuggestionSource> _sources = [];

    private volatile FrozenDictionary<string, ISuggestionSource> _byName = FrozenDictionary<
        string,
        ISuggestionSource
    >.Empty;

    public bool TryFind(string name, out ISuggestionSource source) =>
        _byName.TryGetValue(name, out source!);

    public IDisposable Register(IEnumerable<ISuggestionSource> sources)
    {
        var batch = sources.ToList();

        lock (_gate)
        {
            var taken = new HashSet<string>(_sources.Select(x => x.Name), StringComparer.Ordinal);

            foreach (var source in batch)
                if (!taken.Add(source.Name))
                    throw new InvalidOperationException(
                        $"Suggestion source '{source.Name}' of {source.GetType().Name} is already registered."
                    );

            _sources.AddRange(batch);
            Rebuild();
        }

        return new Removal(this, batch);
    }

    private void Remove(IReadOnlyList<ISuggestionSource> batch)
    {
        lock (_gate)
        {
            foreach (var source in batch)
                _sources.Remove(source);

            Rebuild();
        }
    }

    /// <summary>Builds the lookup; callers hold the gate.</summary>
    private void Rebuild() =>
        _byName = _sources.ToFrozenDictionary(x => x.Name, StringComparer.Ordinal);

    private sealed class Removal(
        SuggestionSourceRegistry registry,
        IReadOnlyList<ISuggestionSource> batch
    ) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                registry.Remove(batch);
        }
    }
}
