using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture;

namespace Turbo.Furniture;

public sealed class StorageData : IStorageData
{
    public Dictionary<string, int> Storage { get; set; } = [];

    private Func<Task>? _onChanged;

    public bool TryGet(string key, out int value) => Storage.TryGetValue(key, out value);

    public void SetValue(string key, int value)
    {
        Storage.Add(key, value);

        _ = _onChanged?.Invoke();
    }

    public void Remove(string key)
    {
        if (!Storage.Remove(key))
            _ = _onChanged?.Invoke();
    }

    public void SetAction(Func<Task>? onChanged) => _onChanged = onChanged;
}
