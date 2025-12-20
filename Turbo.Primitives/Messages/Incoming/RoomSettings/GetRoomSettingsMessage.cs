using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Messages.Incoming.RoomSettings;

public record GetRoomSettingsMessage : IMessageEvent
{
    public RoomId RoomId { get; init; }
}
