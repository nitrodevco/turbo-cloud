using System.Collections.Generic;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Grains.Storage;

public sealed class PlayerActiveStore : VariableStore
{
    private readonly Dictionary<
        PlayerId,
        Dictionary<WiredVariableKey, WiredVariableValue>
    > _byPlayerId = [];

    public bool RemovePlayerStore(PlayerId playerId) => _byPlayerId.Remove(playerId);

    public override bool TryGetStore(
        WiredVariableKey key,
        out Dictionary<WiredVariableKey, WiredVariableValue> store
    )
    {
        store = [];

        if (key.TargetType != WiredVariableTargetType.User)
            return false;

        var targetId = PlayerId.Parse(key.TargetId);

        if (!_byPlayerId.TryGetValue(targetId, out var found))
        {
            found = [];

            _byPlayerId[targetId] = found;
        }

        store = found;

        return true;
    }
}
