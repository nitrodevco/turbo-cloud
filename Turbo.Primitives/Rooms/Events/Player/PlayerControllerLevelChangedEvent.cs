using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Events.Player;

/// <summary>
/// A player's standing in the room was worked out again: on entry, and when rights, ownership
/// or group membership change. Systems whose permissions follow the level listen for it.
/// </summary>
[GenerateSerializer]
public sealed record PlayerControllerLevelChangedEvent : PlayerEvent
{
    [Id(0)]
    public required RoomControllerType ControllerLevel { get; init; }
}
