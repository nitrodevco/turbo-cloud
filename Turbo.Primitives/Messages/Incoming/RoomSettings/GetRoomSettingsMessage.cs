using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.RoomSettings;

public record GetRoomSettingsMessage : IMessageEvent
{
    public int RoomId { get; init; }
}
