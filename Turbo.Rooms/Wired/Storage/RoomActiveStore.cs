using System;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Wired.Storage;

/// <summary>What room variables hold: one store, on the room's clock.</summary>
public sealed class RoomActiveStore(TimeProvider clock) : ActiveStore
{
    private readonly KeyValueStore _vars = new() { Clock = clock };

    public override bool TryGetStore(WiredVariableKey key, out KeyValueStore? store)
    {
        store = _vars;

        return true;
    }
}
