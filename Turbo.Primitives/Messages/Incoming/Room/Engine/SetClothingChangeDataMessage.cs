using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

/// <summary>Dresses a clothing booth: the look it gives one gender.</summary>
public record SetClothingChangeDataMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
    public required string Gender { get; init; }
    public required string Figure { get; init; }
}
