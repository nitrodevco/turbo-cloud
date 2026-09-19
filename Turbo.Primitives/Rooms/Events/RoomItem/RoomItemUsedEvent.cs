using Orleans;

namespace Turbo.Primitives.Rooms.Events.RoomItem;

/// <summary>A player double-clicked (used) an item. Feeds the "user uses furni" wired trigger.</summary>
[GenerateSerializer]
public sealed record RoomItemUsedEvent : RoomItemEvent;
