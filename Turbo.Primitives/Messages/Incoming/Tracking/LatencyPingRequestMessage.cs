using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Tracking;

public record LatencyPingRequestMessage : IMessageEvent
{
    public int RequestId { get; init; }
}
