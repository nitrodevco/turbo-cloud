using System.Collections.Generic;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Grains.Storage;

public sealed class PlayerActiveStore : ActiveStore
{
    private readonly Dictionary<PlayerId, KeyValueStore> _byPlayerId = [];

    public bool RemovePlayerStore(PlayerId playerId) => _byPlayerId.Remove(playerId);

    public override bool TryGetStore(WiredVariableKey key, out KeyValueStore? store)
    {
        store = null;

        if (key.TargetType != WiredVariableTargetType.User)
            return false;

        var targetId = PlayerId.Parse(key.TargetId);

        if (!_byPlayerId.TryGetValue(targetId, out var found))
        {
            found = new KeyValueStore();

            _byPlayerId[targetId] = found;
        }

        store = found;

        return true;
    }
}
