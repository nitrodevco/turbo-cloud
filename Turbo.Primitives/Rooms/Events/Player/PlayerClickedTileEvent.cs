namespace Turbo.Primitives.Rooms.Events.Player;

public sealed record PlayerClickedTileEvent : PlayerEvent
{
    public int TileX { get; init; }
    public int TileY { get; init; }
}
