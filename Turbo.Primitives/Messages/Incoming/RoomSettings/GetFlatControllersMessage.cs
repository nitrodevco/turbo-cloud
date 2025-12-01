using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.RoomSettings;

public record GetFlatControllersMessage : IMessageEvent
{
    public int RoomId { get; init; }
}
