using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Furniture;

/// <summary>The owner named the pet in a package.</summary>
public record OpenPetPackageMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
    public required string Name { get; init; }
}
