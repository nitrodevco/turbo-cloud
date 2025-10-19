using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record ForwardToARandomPromotedRoomMessage : IMessageEvent
{
    public string? Category { get; init; }
}
