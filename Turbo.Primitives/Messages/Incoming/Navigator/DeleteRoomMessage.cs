using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record DeleteRoomMessage : IMessageEvent
{
    public int RoomId { get; init; }
}
