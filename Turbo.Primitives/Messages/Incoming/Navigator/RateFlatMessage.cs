using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record RateFlatMessage : IMessageEvent
{
    public int Points { get; init; }
}
