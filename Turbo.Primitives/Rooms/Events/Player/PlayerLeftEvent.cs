using Orleans;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Events.Player;

/// <summary>
/// A player left the room. <see cref="ObjectId"/> is the avatar they were, which is how
/// anything keyed by avatar (the wired user variables) finds what to let go of.
/// </summary>
[GenerateSerializer]
public sealed record PlayerLeftEvent : PlayerEvent
{
    [Id(0)]
    public required RoomObjectId ObjectId { get; init; }
}
