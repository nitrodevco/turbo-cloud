using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record SetRoomSessionTagsMessage : IMessageEvent
{
    public string? Tag1 { get; init; }
    public string? Tag2 { get; init; }
}
