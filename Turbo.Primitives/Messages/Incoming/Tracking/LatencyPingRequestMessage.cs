using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Tracking;

public record LatencyPingRequestMessage : IMessageEvent
{
    public int Id { get; init; }
}
