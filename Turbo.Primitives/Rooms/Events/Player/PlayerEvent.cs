using Turbo.Primitives.Players;

namespace Turbo.Primitives.Rooms.Events.Player;

public abstract record PlayerEvent : RoomEvent
{
    public required PlayerId PlayerId { get; init; }
}
