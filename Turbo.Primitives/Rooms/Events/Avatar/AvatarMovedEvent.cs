using Orleans;

namespace Turbo.Primitives.Rooms.Events.Avatar;

/// <summary>
/// An avatar's tile changed: a walked step, a teleport, being pushed or carried, or a roller. It
/// is published once per change at the point the avatar's position is updated, never for a tick
/// in which the avatar stayed where it was. It names the avatar by room index; for a player,
/// <see cref="RoomEvent.CausedBy"/> carries the player id.
/// <para>
/// Observer events like this one go to <see cref="IRoomEventListenerRegistry"/> listeners only,
/// not to the room's own systems, so a busy room pays nothing for them when none is registered.
/// </para>
/// </summary>
[GenerateSerializer]
public sealed record AvatarMovedEvent : AvatarEvent
{
    [Id(0)]
    public required int FromX { get; init; }

    [Id(1)]
    public required int FromY { get; init; }

    [Id(2)]
    public required int ToX { get; init; }

    [Id(3)]
    public required int ToY { get; init; }
}
