using Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Rooms.Events.Player;

[GenerateSerializer]
public abstract record PlayerEvent : RoomEvent
{
    [Id(0)]
    public required PlayerId PlayerId { get; init; }
}
