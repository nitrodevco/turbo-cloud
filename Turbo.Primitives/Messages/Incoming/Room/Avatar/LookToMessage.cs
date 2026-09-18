using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Avatar;

public record LookToMessage : IMessageEvent
{
    public required int X { get; init; }
    public required int Y { get; init; }
}
