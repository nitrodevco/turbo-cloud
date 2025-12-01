using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record GetOfficialRoomsMessage : IMessageEvent
{
    public int AdIndex { get; init; }
}
