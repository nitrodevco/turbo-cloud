using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Grains.Storage;

public sealed class FurnitureActiveStore : ActiveStore
{
    private readonly Dictionary<RoomObjectId, KeyValueStore> _byItemId = [];

    public bool RemoveFurnitureStore(RoomObjectId objectId) => _byItemId.Remove(objectId);

    public override bool TryGetStore(WiredVariableKey key, out KeyValueStore? store)
    {
        store = null;

        if (key.TargetType != WiredVariableTargetType.Furni)
            return false;

        var targetId = RoomObjectId.Parse(key.TargetId);

        if (!_byItemId.TryGetValue(targetId, out var found))
        {
            found = new KeyValueStore();

            _byItemId[targetId] = found;
        }

        store = found;

        return true;
    }
}
