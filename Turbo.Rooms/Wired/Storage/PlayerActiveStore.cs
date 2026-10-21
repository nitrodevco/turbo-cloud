using System;
using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Wired.Storage;

/// <summary>
/// What user variables hold, one store per avatar. Keyed by the avatar's room index, as every
/// user variable is, so that a pet and a bot can hold values too; a player's own id is a
/// value a variable reports, never the key. Each store is on the room's clock.
/// </summary>
public sealed class PlayerActiveStore(TimeProvider clock) : ActiveStore
{
    private readonly Dictionary<RoomObjectId, KeyValueStore> _byObjectId = [];

    public bool RemoveAvatarStore(RoomObjectId objectId) => _byObjectId.Remove(objectId);

    public override bool TryGetStore(WiredVariableKey key, out KeyValueStore? store)
    {
        store = null;

        if (key.TargetType != WiredVariableTargetType.User)
            return false;

        var objectId = RoomObjectId.Parse(key.TargetId);

        if (!_byObjectId.TryGetValue(objectId, out var found))
        {
            found = new KeyValueStore { Clock = clock };

            _byObjectId[objectId] = found;
        }

        store = found;

        return true;
    }
}
