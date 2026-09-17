using Orleans;

namespace Turbo.Primitives.Rooms.Events.Player;

[GenerateSerializer]
public sealed record PlayerClickedTileEvent : PlayerEvent
{
    [Id(0)]
    public int TileX { get; init; }

    [Id(1)]
    public int TileY { get; init; }
}
