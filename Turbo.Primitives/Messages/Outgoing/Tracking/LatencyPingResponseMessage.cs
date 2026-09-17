using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Tracking;

[GenerateSerializer, Immutable]
public sealed record LatencyPingResponseMessage : IComposer
{
    [Id(0)]
    public int RequestId { get; init; }
}
