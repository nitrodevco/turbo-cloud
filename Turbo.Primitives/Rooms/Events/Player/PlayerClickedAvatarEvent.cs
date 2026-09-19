using Orleans;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Events.Player;

/// <summary>A player clicked another avatar in the room.</summary>
[GenerateSerializer]
public sealed record PlayerClickedAvatarEvent : PlayerEvent
{
    [Id(0)]
    public required RoomObjectId TargetObjectId { get; init; }
}
