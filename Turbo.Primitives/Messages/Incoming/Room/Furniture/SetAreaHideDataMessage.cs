using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Furniture;

public record SetAreaHideDataMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
    public required int RootX { get; init; }
    public required int RootY { get; init; }
    public required int Width { get; init; }
    public required int Length { get; init; }
    public required bool Invisibility { get; init; }
    public required bool WallItems { get; init; }
    public required bool Invert { get; init; }
}
