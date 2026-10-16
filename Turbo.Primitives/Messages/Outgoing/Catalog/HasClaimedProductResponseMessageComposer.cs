using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

[GenerateSerializer, Immutable]
public sealed record HasClaimedProductResponseMessageComposer : IComposer
{
    [Id(0)]
    public required string ClaimId { get; init; }

    [Id(1)]
    public required bool HasClaimed { get; init; }
}
