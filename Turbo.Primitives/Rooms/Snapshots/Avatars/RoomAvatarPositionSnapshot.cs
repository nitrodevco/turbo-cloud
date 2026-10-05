using Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Snapshots.Avatars;

/// <summary>
/// Where a player's avatar stands in a room right now, for callers outside the room's own turn
/// (plugins, background services). Only players have one; bots and pets are never asked for.
/// </summary>
[GenerateSerializer, Immutable]
public record RoomAvatarPositionSnapshot
{
    [Id(0)]
    public required RoomId RoomId { get; init; }

    [Id(1)]
    public required int X { get; init; }

    [Id(2)]
    public required int Y { get; init; }

    /// <summary>The height the avatar stands at on its tile.</summary>
    [Id(3)]
    public required Altitude Z { get; init; }

    /// <summary>The way the body faces.</summary>
    [Id(4)]
    public required Rotation Rotation { get; init; }

    /// <summary>Asleep from idling; reading the position does not wake the avatar.</summary>
    [Id(5)]
    public required bool IsIdle { get; init; }

    [Id(6)]
    public required bool IsWalking { get; init; }
}
