using Turbo.Primitives.Rooms.Object.Furniture;

namespace Turbo.Primitives.Rooms;

/// <summary>
/// A room system that caps how much of its own kind of furni a room may hold. The placement
/// path asks every registered limit and knows none of them: the system that owns the kind
/// recognises its items, counts them and refuses with its own error.
/// </summary>
public interface IRoomPlacementLimit
{
    /// <summary>Throws a <c>TurboException</c> when the room cannot take this item.</summary>
    public void EnsureCanPlace(IRoomItem item);
}
