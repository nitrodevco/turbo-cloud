using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Avatar;

public record DanceMessage : IMessageEvent
{
    public int DanceId { get; init; }
}
