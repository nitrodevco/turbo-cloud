using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Inventory.Trading;

/// <summary>Open a trade with the avatar at this room object id.</summary>
public record OpenTradingMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
}
