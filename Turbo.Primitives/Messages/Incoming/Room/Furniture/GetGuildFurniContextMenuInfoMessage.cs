using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Furniture;

public record GetGuildFurniContextMenuInfoMessage : IMessageEvent
{
    public RoomObjectId ObjectId { get; init; }

    /// <summary>
    /// Which of the two object categories the client is asking about, floor or wall. Guild furni
    /// is always a floor item, so it is read and ignored rather than trusted.
    /// </summary>
    public int Category { get; init; }
}
