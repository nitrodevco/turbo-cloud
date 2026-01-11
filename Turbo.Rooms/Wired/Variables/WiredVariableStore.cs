using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Turbo.Rooms.Wired.Variables;

public sealed class WiredVariableStore
{
    private readonly Dictionary<string, int> _storage = [];

    private Func<Task>? _onChanged;
    private bool _dirty = true;

    public void SetAction(Func<Task>? onChanged) => _onChanged = onChanged;

    public bool TryGet(in WiredVariableStorageKey key, out WiredValue value)
    {
        value = default!;
        return false;
    }

    public void Set(in WiredVariableStorageKey key, WiredValue value) { }

    public bool Remove(in WiredVariableStorageKey key)
    {
        return false;
    }

    private void MarkDirty()
    {
        _dirty = true;

        _ = _onChanged?.Invoke();
    }
}
