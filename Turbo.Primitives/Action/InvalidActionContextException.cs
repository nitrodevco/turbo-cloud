using System;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Action;

/// <summary>
/// Raised when an <see cref="ActionContext"/> is requested for a room object context that has no
/// action origin, such as a non-player object.
/// </summary>
public sealed class InvalidActionContextException(Type roomObjectContextType)
    : Exception(
        $"An action context cannot be created for room object context '{roomObjectContextType.Name}'."
    )
{
    /// <summary>The <see cref="IRoomObjectContext"/> implementation that was rejected.</summary>
    public Type RoomObjectContextType { get; } = roomObjectContextType;
}
