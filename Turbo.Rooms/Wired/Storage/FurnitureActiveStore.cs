using System;
using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Wired.Storage;

/// <summary>What furni variables hold, one store per furni, each on the room's clock.</summary>
public sealed class FurnitureActiveStore(TimeProvider clock) : ActiveStore
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
            found = new KeyValueStore { Clock = clock };

            _byItemId[targetId] = found;
        }

        store = found;

        return true;
    }
}
