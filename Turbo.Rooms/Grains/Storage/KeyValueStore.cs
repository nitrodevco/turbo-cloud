using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Grains.Storage;

public sealed class KeyValueStore : IWiredVariableStore
{
    public Dictionary<string, WiredVariableValue> Store { get; set; } = [];

    /// <summary>Creation and last write times per key, for the variable age wired.</summary>
    public Dictionary<string, WiredVariableTimestamps> Timestamps { get; set; } = [];

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
        var existed = Store.ContainsKey(key.ToStorageKey());

        if (existed && !replace)
            return Task.FromResult(false);

        Store[key.ToStorageKey()] = value;
        Stamp(key.ToStorageKey(), !existed);

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
        Stamp(key.ToStorageKey(), false);

        MarkDirty();

        return Task.FromResult(true);
    }

    public bool TryGetTimestamps(
        in WiredVariableKey key,
        out long createdAtMs,
        out long updatedAtMs
    )
    {
        createdAtMs = 0;
        updatedAtMs = 0;

        if (!Timestamps.TryGetValue(key.ToStorageKey(), out var stamps))
            return Store.ContainsKey(key.ToStorageKey());

        createdAtMs = stamps.CreatedAtMs;
        updatedAtMs = stamps.UpdatedAtMs;

        return true;
    }

    public bool RemoveValue(WiredVariableKey key)
    {
        if (!Store.ContainsKey(key.ToStorageKey()) || !Store.Remove(key.ToStorageKey()))
            return false;

        Timestamps.Remove(key.ToStorageKey());

        MarkDirty();

        return true;
    }

    private void Stamp(string storageKey, bool created)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (created || !Timestamps.TryGetValue(storageKey, out var stamps))
            Timestamps[storageKey] = new WiredVariableTimestamps(now, now);
        else
            Timestamps[storageKey] = stamps with { UpdatedAtMs = now };
    }

    private void MarkDirty()
    {
        _ = _onChanged?.Invoke();
    }
}
