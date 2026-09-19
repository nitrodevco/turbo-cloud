using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

/// <summary>Move or turn a placed bot, addressed by its room object id.</summary>
public record MoveEntityInFlatMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
    public required Rotation Rotation { get; init; }
}
