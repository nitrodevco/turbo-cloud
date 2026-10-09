using Orleans;

namespace Turbo.Primitives.Rooms.Events.RoomItem;

[GenerateSerializer]
public sealed record RoomItemMovedEvent : RoomItemEvent
{
    [Id(0)]
    public required int PrevIdx { get; init; }

    /// <summary>
    /// Whether the item left its tile: false for a floor item turned in place, which a reward
    /// track counts as a rotation rather than a move. A wall item's move is always a move.
    /// </summary>
    [Id(1)]
    public bool TileChanged { get; init; } = true;
}
