using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Tracking;

/// <summary>The latency test, every latencytest.interval ms (LatencyTracker.as:87).</summary>
public record LatencyPingRequestMessage : IClientTimerMessage
{
    public int RequestId { get; init; }
}
