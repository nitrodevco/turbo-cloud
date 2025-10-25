using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Tracking;

public record LatencyPingResponseMessage : IComposer
{
    public int RequestId { get; init; }
}
