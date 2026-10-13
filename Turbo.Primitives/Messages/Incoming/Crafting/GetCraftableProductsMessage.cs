using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Crafting;

public record GetCraftableProductsMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
}
