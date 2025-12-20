using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record DeleteFavouriteRoomMessage : IMessageEvent
{
    public RoomId RoomId { get; init; }
}
