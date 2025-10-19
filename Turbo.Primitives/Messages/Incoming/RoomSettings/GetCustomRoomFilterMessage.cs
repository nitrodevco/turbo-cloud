using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.RoomSettings;

public record GetCustomRoomFilterMessage : IMessageEvent
{
    public int RoomId { get; init; }
}
