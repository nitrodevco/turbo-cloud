using Orleans;

namespace Turbo.Primitives.Rooms.Events.RoomItem;

[GenerateSerializer]
public sealed record RoomItemPlacedEvent : RoomItemEvent
{
    /// <summary>
    /// Whether the item is one the Builders Club lends, which a reward track counts as placing
    /// Builders Club furni rather than placing furni.
    /// </summary>
    [Id(0)]
    public bool IsBuildersClub { get; init; }
}
