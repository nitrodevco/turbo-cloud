using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record AddFavouriteRoomMessage : IMessageEvent
{
    public int RoomId { get; init; }
}
