using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record ForwardToARandomPromotedRoomMessage : IMessageEvent
{
    public required string Category { get; init; }
}
