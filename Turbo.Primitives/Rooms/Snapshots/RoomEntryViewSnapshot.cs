using System.Collections.Generic;
using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.Primitives.Rooms.Snapshots;

/// <summary>
/// Everything a player walking in is shown of the room, taken in one turn of the room grain:
/// entering asked for it in nine calls, and the room could change between any two of them.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record RoomEntryViewSnapshot
{
    [Id(0)]
    public required RoomSnapshot Room { get; init; }

    [Id(1)]
    public required RoomMapSnapshot Map { get; init; }

    [Id(2)]
    public required ImmutableDictionary<PlayerId, string> OwnerNames { get; init; }

    [Id(3)]
    public required ImmutableArray<RoomFloorItemSnapshot> FloorItems { get; init; }

    [Id(4)]
    public required ImmutableArray<RoomWallItemSnapshot> WallItems { get; init; }

    [Id(5)]
    public required ImmutableArray<RoomAvatarSnapshot> Avatars { get; init; }

    [Id(6)]
    public required ImmutableArray<KeyValuePair<RoomPropertyType, string>> Properties { get; init; }

    /// <summary>Whether the arriving player may still rate the room.</summary>
    [Id(7)]
    public required bool CanRate { get; init; }

    [Id(8)]
    public RoomEventSnapshot? ActiveEvent { get; init; }

    [Id(9)]
    public required bool IsMuted { get; init; }
}
