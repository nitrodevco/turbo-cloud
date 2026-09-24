using Orleans;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Guilds.Snapshots;

/// <summary>
/// A room the player owns, offered as a homeroom. <see cref="HasControllers"/> is why this is
/// not just a room summary: the client warns that a room with rights given out will have them
/// overridden by the group's, and that cannot be undone.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GuildRoomOptionSnapshot
{
    [Id(0)]
    public required RoomId RoomId { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    /// <summary>Whether anybody has been given rights in this room.</summary>
    [Id(2)]
    public required bool HasControllers { get; init; }
}
