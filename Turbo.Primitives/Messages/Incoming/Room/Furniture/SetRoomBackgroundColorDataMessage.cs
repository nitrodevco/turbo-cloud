using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Furniture;

public record SetRoomBackgroundColorDataMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
    public required int Hue { get; init; }
    public required int Saturation { get; init; }
    public required int Lightness { get; init; }
}
