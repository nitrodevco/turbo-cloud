using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Tracking;

public sealed record LatencyPingResponseMessage : IComposer
{
    public int RequestId { get; init; }
}
