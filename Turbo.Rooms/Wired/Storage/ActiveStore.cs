using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Wired.Storage;

/// <summary>
/// Finds the store a key's values live in: one per furni, one per avatar, one for the room.
/// It only routes. Reading and writing go through the variable box, which applies its flags
/// (<c>CanCreateAndDelete</c>, what it may bind to) and tells the room of the change; a
/// read/write surface here as well skipped all of that, and nothing called it.
/// </summary>
public abstract class ActiveStore
{
    public abstract bool TryGetStore(WiredVariableKey key, out KeyValueStore? store);
}
