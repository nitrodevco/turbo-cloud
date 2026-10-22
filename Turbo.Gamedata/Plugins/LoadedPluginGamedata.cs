using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Turbo.Gamedata.Plugins;

/// <summary>
/// The plugins loaded in this process (<see cref="Turbo.Primitives.Plugins.IPluginGamedataSeeds.Loaded"/>):
/// the external variables and texts a plugin owns are written into the client's files only while
/// it is one of them (<see cref="Files.GamedataFileService"/>).
/// </summary>
internal sealed class LoadedPluginGamedata
{
    private readonly ConcurrentDictionary<string, int> _loaded = new(
        StringComparer.OrdinalIgnoreCase
    );

    public bool IsLoaded(string pluginKey) =>
        _loaded.TryGetValue(pluginKey, out var count) && count > 0;

    /// <summary>The plugin is loaded until the handle is disposed; <paramref name="changed"/> runs after each.</summary>
    public IDisposable Add(string pluginKey, Action changed)
    {
        _loaded.AddOrUpdate(pluginKey, 1, (_, count) => count + 1);
        changed();

        return new Handle(() =>
        {
            if (_loaded.AddOrUpdate(pluginKey, 0, (_, count) => count - 1) <= 0)
                _loaded.TryRemove(KeyValuePair.Create(pluginKey, 0));

            changed();
        });
    }

    private sealed class Handle(Action release) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                release();
        }
    }
}
