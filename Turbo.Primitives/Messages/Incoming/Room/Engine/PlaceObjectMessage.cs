using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

public record PlaceObjectMessage : IMessageEvent
{
    public required string Data { get; init; }
}
