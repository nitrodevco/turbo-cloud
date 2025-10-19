using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record RemoveOwnRoomRightsRoomMessageMessage : IMessageEvent
{
    public int RoomId { get; init; }
}
