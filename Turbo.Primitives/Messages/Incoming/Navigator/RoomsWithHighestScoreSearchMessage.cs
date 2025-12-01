using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record RoomsWithHighestScoreSearchMessage : IMessageEvent
{
    public int AdIndex { get; init; }
}
