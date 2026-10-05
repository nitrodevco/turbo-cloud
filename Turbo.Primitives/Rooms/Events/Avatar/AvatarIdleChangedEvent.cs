using Orleans;

namespace Turbo.Primitives.Rooms.Events.Avatar;

/// <summary>
/// An avatar fell asleep after the room's idle timeout (<see cref="IsIdle"/> true) or woke up
/// through its next action (false). Published on those transitions only. Like
/// <see cref="AvatarMovedEvent"/> it goes to <see cref="IRoomEventListenerRegistry"/> listeners
/// only, and for a player <see cref="RoomEvent.CausedBy"/> carries the player id.
/// </summary>
[GenerateSerializer]
public sealed record AvatarIdleChangedEvent : AvatarEvent
{
    [Id(0)]
    public required bool IsIdle { get; init; }
}
