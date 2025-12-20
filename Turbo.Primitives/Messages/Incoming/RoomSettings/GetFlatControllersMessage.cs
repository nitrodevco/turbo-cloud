using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Messages.Incoming.RoomSettings;

public record GetFlatControllersMessage : IMessageEvent
{
    public RoomId RoomId { get; init; }
}
