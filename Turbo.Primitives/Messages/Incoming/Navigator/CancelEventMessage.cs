using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record CancelEventMessage : IMessageEvent
{
    public int AdvertisementId { get; init; }
}
