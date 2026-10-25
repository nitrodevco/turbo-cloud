using Orleans;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Events.RoomItem;

/// <summary>
/// A wired-moved furni ran into a user: the furni is the item, the user (a player or a bot) the
/// one who triggers.
/// </summary>
[GenerateSerializer]
public sealed record RoomItemCollisionEvent : RoomItemEvent
{
    [Id(0)]
    public required RoomObjectId AvatarObjectId { get; init; }
}
