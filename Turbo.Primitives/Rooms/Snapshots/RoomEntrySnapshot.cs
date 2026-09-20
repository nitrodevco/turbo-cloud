using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Snapshots;

/// <summary>
/// How a player is arriving in a room. A furni that sends someone to another room says so
/// before it forwards them, and the room they land in keeps it on their avatar for the wired
/// <c>@room_entry.*</c> variables to read.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record RoomEntrySnapshot
{
    public static RoomEntrySnapshot Default { get; } =
        new() { Method = RoomEntryMethodType.Default, TeleportId = 0 };

    [Id(0)]
    public required RoomEntryMethodType Method { get; init; }

    /// <summary>
    /// The teleporter they arrive at, when they came through one; zero otherwise. It is the
    /// furni in this room, the far half of the pair they stepped into.
    /// </summary>
    [Id(1)]
    public required int TeleportId { get; init; }
}
