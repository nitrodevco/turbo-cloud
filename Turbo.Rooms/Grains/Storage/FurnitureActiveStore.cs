using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Grains.Storage;

public sealed class FurnitureActiveStore : VariableStore
{
    private readonly Dictionary<
        RoomObjectId,
        Dictionary<WiredVariableKey, WiredVariableValue>
    > _byItemId = [];

    public bool RemoveFurnitureStore(RoomObjectId objectId) => _byItemId.Remove(objectId);

    public override bool TryGetStore(
        WiredVariableKey key,
        out Dictionary<WiredVariableKey, WiredVariableValue> store
    )
    {
        store = [];

        if (key.TargetType != WiredVariableTargetType.Furni)
            return false;

        var targetId = RoomObjectId.Parse(key.TargetId);

        if (!_byItemId.TryGetValue(targetId, out var found))
        {
            found = [];

            _byItemId[targetId] = found;
        }

        store = found;

        return true;
    }
}
