using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Grains.Storage;

public sealed class RoomActiveStore : ActiveStore
{
    private readonly KeyValueStore _vars = new();

    public override bool TryGetStore(WiredVariableKey key, out KeyValueStore? store)
    {
        store = _vars;

        return true;
    }
}
