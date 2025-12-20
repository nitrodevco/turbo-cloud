using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Messages.Incoming.Room.Session;

public record OpenFlatConnectionMessage : IMessageEvent
{
    public RoomId RoomId { get; init; }
    public string Password { get; init; } = string.Empty;
    public int Unknown1 { get; init; }
}
