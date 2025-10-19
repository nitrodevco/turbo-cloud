using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record RemoveOwnRoomRightsRoomMessage : IMessageEvent
{
    public int RoomId { get; init; }
}
