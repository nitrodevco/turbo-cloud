using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

[GenerateSerializer, Immutable]
public sealed record UserObjectMessage : IComposer
{
    [Id(0)]
    public required PlayerSummarySnapshot Player { get; init; }
}
