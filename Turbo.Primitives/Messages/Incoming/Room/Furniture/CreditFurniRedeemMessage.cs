using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Furniture;

public record CreditFurniRedeemMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
}
