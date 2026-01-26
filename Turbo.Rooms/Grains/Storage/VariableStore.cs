using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Grains.Storage;

public abstract class VariableStore : IWiredVariableStore
{
    public bool TryGetValue(in WiredVariableKey key, out WiredVariableValue value)
    {
        value = WiredVariableValue.Default;

        if (!TryGetStore(key, out var store))
            return false;

        return store.TryGetValue(key, out value);
    }

    public Task<bool> GiveValueAsync(
        WiredVariableKey key,
        WiredVariableValue value,
        bool replace = false
    )
    {
        if (!TryGetStore(key, out var store) || (store.ContainsKey(key) && !replace))
            return Task.FromResult(false);

        store[key] = value;

        return Task.FromResult(true);
    }

    public Task<bool> SetValueAsync(
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableValue value
    )
    {
        if (!TryGetStore(key, out var store) || !store.ContainsKey(key))
            return Task.FromResult(false);

        store[key] = value;

        return Task.FromResult(true);
    }

    public bool RemoveValue(WiredVariableKey key)
    {
        if (!TryGetStore(key, out var store))
            return false;

        return store.Remove(key);
    }

    public abstract bool TryGetStore(
        WiredVariableKey key,
        out Dictionary<WiredVariableKey, WiredVariableValue> store
    );
}
