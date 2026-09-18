using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Friendfurni;

public record FriendFurniConfirmLockMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
    public required bool Confirmed { get; init; }
}
