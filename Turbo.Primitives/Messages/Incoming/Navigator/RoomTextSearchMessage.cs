using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record RoomTextSearchMessage : IMessageEvent
{
    public string? Query { get; init; }
}
