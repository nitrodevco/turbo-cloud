using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Register;

public record UpdateFigureDataMessage : IMessageEvent
{
    public required string Figure { get; init; }
    public required string Gender { get; init; }
}
