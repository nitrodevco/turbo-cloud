using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Furniture;

public record OpenMysteryTrophyMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
    public required string Inscription { get; init; }
}
