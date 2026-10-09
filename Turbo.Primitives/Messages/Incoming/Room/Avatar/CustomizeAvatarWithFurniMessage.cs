using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Avatar;

/// <summary>Binds the clothing furni in the room to its owner (<c>CustomizeAvatarWithFurniMessageComposer</c>).</summary>
public record CustomizeAvatarWithFurniMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
}
