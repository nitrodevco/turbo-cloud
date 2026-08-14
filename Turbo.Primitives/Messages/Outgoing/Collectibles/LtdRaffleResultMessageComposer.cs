using Orleans;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Collectibles;

[GenerateSerializer, Immutable]
public sealed record LtdRaffleResultMessageComposer : IComposer
{
    [Id(0)]
    public required string ClassName { get; init; }

    [Id(1)]
    public required LtdRaffleResultType ResultCode { get; init; }
}
