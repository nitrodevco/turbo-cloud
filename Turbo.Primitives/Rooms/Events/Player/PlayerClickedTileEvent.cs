using Orleans;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Events.Player;

/// <summary>A player clicked a tile holding an invisible click tile; one event per click tile there.</summary>
[GenerateSerializer]
public sealed record PlayerClickedTileEvent : PlayerEvent
{
    [Id(0)]
    public int TileX { get; init; }

    [Id(1)]
    public int TileY { get; init; }

    /// <summary>The invisible click tile, which the "user clicks tile" trigger picks.</summary>
    [Id(2)]
    public required RoomObjectId FurniId { get; init; }
}
