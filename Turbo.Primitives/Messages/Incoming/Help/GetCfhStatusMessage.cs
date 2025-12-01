using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Help;

public record GetCfhStatusMessage : IMessageEvent
{
    public bool Flag { get; init; }
}
