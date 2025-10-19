using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record GetGuestRoomMessage : IMessageEvent
{
    public int RoomId { get; init; }
    public bool EnterRoom { get; init; }
    public bool RoomForward { get; init; }
}
