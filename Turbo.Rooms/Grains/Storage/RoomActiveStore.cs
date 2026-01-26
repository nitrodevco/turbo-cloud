using System.Collections.Generic;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Grains.Storage;

public sealed class RoomActiveStore : VariableStore
{
    private readonly Dictionary<WiredVariableKey, WiredVariableValue> _vars = [];

    public override bool TryGetStore(
        WiredVariableKey key,
        out Dictionary<WiredVariableKey, WiredVariableValue> store
    )
    {
        store = _vars;

        return true;
    }
}
