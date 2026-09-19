using System.Collections.Immutable;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Inventory.Trading;

/// <summary>Several items of the same kind offered at once.</summary>
public record AddItemsToTradeMessage : IMessageEvent
{
    public required ImmutableArray<RoomObjectId> ItemIds { get; init; }
}
