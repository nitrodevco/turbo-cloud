using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Grains.Storage;

public sealed class KeyValueStore : IWiredVariableStore
{
    public Dictionary<string, WiredVariableValue> Store { get; set; } = [];

    private Func<Task>? _onChanged;

    public void SetAction(Func<Task>? onChanged) => _onChanged = onChanged;

    public bool ContainsKey(WiredVariableKey key) => Store.ContainsKey(key.ToStorageKey());

    public bool TryGetValue(in WiredVariableKey key, out WiredVariableValue value) =>
        Store.TryGetValue(key.ToStorageKey(), out value);

    public Task<bool> GiveValueAsync(
        WiredVariableKey key,
        WiredVariableValue value,
        bool replace = false
    )
    {
        if (Store.ContainsKey(key.ToStorageKey()) && !replace)
            return Task.FromResult(false);

        Store[key.ToStorageKey()] = value;

        MarkDirty();

        return Task.FromResult(true);
    }

    public Task<bool> SetValueAsync(
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableValue value
    )
    {
        if (!Store.ContainsKey(key.ToStorageKey()))
            return Task.FromResult(false);

        Store[key.ToStorageKey()] = value;

        MarkDirty();

        return Task.FromResult(true);
    }

    public bool RemoveValue(WiredVariableKey key)
    {
        if (!Store.ContainsKey(key.ToStorageKey()) || !Store.Remove(key.ToStorageKey()))
            return false;

        MarkDirty();

        return true;
    }

    private void MarkDirty()
    {
        _ = _onChanged?.Invoke();
    }
}
