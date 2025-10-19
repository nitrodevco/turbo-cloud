using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.RoomSettings;

public record GetBannedUsersFromRoomMessage : IMessageEvent
{
    public int RoomId { get; init; }
}
