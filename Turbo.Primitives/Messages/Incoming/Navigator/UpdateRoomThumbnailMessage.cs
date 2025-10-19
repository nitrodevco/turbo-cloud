using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record UpdateRoomThumbnailMessage : IMessageEvent
{
    public int RoomId { get; init; }
}
