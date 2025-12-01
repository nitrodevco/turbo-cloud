using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record ConvertGlobalRoomIdMessage : IMessageEvent
{
    public required string FlatId { get; init; }
}
