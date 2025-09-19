using System;
using System.Collections.Concurrent;

namespace Turbo.Plugins.Exports;

public sealed class ExportRegistry
{
    private readonly ConcurrentDictionary<(Type t, string key), object> _map = new();

    public ReloadableExport<T> GetOrCreate<T>(string exportKey)
        where T : class =>
        (ReloadableExport<T>)_map.GetOrAdd((typeof(T), exportKey), _ => new ReloadableExport<T>());

    public void Swap<T>(string exportKey, T instance)
        where T : class => GetOrCreate<T>(exportKey).Swap(instance);
}
