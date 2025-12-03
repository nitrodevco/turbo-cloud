using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Collectibles;

[GenerateSerializer, Immutable]
public sealed record EmeraldBalanceMessageComposer : IComposer
{
    [Id(0)]
    public required int EmeraldBalance { get; init; }
}
