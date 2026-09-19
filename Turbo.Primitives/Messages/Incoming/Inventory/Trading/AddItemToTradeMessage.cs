using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Inventory.Trading;

public record AddItemToTradeMessage : IMessageEvent
{
    public required RoomObjectId ItemId { get; init; }
}
