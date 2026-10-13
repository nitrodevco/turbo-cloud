using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Preferences;

public record AddToCustomFilterMessage : IMessageEvent
{
    public string Word { get; init; } = string.Empty;
}
