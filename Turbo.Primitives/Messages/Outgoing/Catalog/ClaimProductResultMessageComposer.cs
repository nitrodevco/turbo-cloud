using Orleans;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

[GenerateSerializer, Immutable]
public sealed record ClaimProductResultMessageComposer : IComposer
{
    [Id(0)]
    public required string ClaimId { get; init; }

    [Id(1)]
    public required ProductClaimResult Result { get; init; }
}
