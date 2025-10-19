using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record ConvertGlobalRoomIdMessage : IMessageEvent
{
    public string? FlatId { get; init; }
}
