using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

public record UseFurnitureMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
    public required int Param { get; init; }
}
