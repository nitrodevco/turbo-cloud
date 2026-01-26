using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Grains.Storage;

public abstract class ActiveStore : IWiredVariableStore
{
    public virtual bool TryGetValue(in WiredVariableKey key, out WiredVariableValue value)
    {
        value = WiredVariableValue.Default;

        if (!TryGetStore(key, out var store) || store is null)
            return false;

        return store.TryGetValue(key, out value);
    }

    public virtual Task<bool> GiveValueAsync(
        WiredVariableKey key,
        WiredVariableValue value,
        bool replace = false
    )
    {
        if (
            !TryGetStore(key, out var store)
            || store is null
            || (store.ContainsKey(key) && !replace)
        )
            return Task.FromResult(false);

        return store.GiveValueAsync(key, value, replace);
    }

    public virtual Task<bool> SetValueAsync(
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableValue value
    )
    {
        if (!TryGetStore(key, out var store) || store is null || !store.ContainsKey(key))
            return Task.FromResult(false);

        return store.SetValueAsync(ctx, key, value);
    }

    public virtual bool RemoveValue(WiredVariableKey key)
    {
        if (!TryGetStore(key, out var store) || store is null)
            return false;

        return store.RemoveValue(key);
    }

    public abstract bool TryGetStore(WiredVariableKey key, out KeyValueStore? store);
}
