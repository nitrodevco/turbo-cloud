using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record DeleteFavouriteRoomMessage : IMessageEvent
{
    public int RoomId { get; init; }
}
