using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record RoomAdEventTabAdClickedMessage : IMessageEvent
{
    public int FlatId { get; init; }
    public string? RoomAdName { get; init; }
    public int RoomAdExpiresInMin { get; init; }
}
