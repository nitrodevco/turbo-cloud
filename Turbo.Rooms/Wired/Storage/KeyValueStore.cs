using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Wired.Storage;

public sealed class KeyValueStore : IWiredVariableStore
{
    public Dictionary<string, WiredVariableValue> Store { get; set; } = [];

    /// <summary>Creation and last write times per key, for the variable age wired.</summary>
    public Dictionary<string, WiredVariableTimestamps> Timestamps { get; set; } = [];

    private Func<Task>? _onChanged;

    public void SetAction(Func<Task>? onChanged) => _onChanged = onChanged;

    /// <summary>
    /// The store this one was started from, whose variables it shares: a signalled stack's context
    /// sees and changes its sender's context variables, and so does every other stack the same
    /// sender signalled. Only a variable given here (a new one, or "Override existing variable")
    /// is this store's own and hides the sender's (Wired Faculty variables-info #18, "Context
    /// Variables - Lifetime": they work "just like variables in coding, with scopes").
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public KeyValueStore? Parent { get; init; }

    /// <summary>A scope inside this one, for a stack this execution starts.</summary>
    public KeyValueStore CreateChild() => new() { Parent = this };

    /// <summary>The store in this scope chain that holds the variable, nearest first.</summary>
    private KeyValueStore? Owner(string storageKey)
    {
        for (var store = this; store is not null; store = store.Parent)
        {
            if (store.Store.ContainsKey(storageKey))
                return store;
        }

        return null;
    }

    public bool ContainsKey(WiredVariableKey key) => Owner(key.ToStorageKey()) is not null;

    public bool TryGetValue(in WiredVariableKey key, out WiredVariableValue value)
    {
        value = default;

        return Owner(key.ToStorageKey()) is { } owner
            && owner.Store.TryGetValue(key.ToStorageKey(), out value);
    }

    public Task<bool> GiveValueAsync(
        WiredVariableKey key,
        WiredVariableValue value,
        bool replace = false
    )
    {
        var existed = Owner(key.ToStorageKey()) is not null;

        if (existed && !replace)
            return Task.FromResult(false);

        Store[key.ToStorageKey()] = value;
        // A give creates the variable on its holder, an overwriting one too: official wired restarts
        // its creation time, which is what "Time Utilities" reads as the moment it was given.
        Stamp(key.ToStorageKey(), true);

        MarkDirty();

        return Task.FromResult(true);
    }

    public Task<bool> SetValueAsync(
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableValue value
    )
    {
        if (Owner(key.ToStorageKey()) is not { } owner)
            return Task.FromResult(false);

        owner.Store[key.ToStorageKey()] = value;
        owner.Stamp(key.ToStorageKey(), false);

        owner.MarkDirty();

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

        if (Owner(key.ToStorageKey()) is not { } owner)
            return false;

        if (!owner.Timestamps.TryGetValue(key.ToStorageKey(), out var stamps))
            return true;

        createdAtMs = stamps.CreatedAtMs;
        updatedAtMs = stamps.UpdatedAtMs;

        return true;
    }

    public bool RemoveValue(WiredVariableKey key)
    {
        if (Owner(key.ToStorageKey()) is not { } owner || !owner.Store.Remove(key.ToStorageKey()))
            return false;

        owner.Timestamps.Remove(key.ToStorageKey());

        owner.MarkDirty();

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
