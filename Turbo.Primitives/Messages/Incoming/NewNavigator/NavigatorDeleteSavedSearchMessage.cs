using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.NewNavigator;

public record NavigatorDeleteSavedSearchMessage : IMessageEvent
{
    public int SearchId { get; init; }
}
