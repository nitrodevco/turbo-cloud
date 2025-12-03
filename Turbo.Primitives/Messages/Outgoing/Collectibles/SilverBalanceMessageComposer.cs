using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Collectibles;

[GenerateSerializer, Immutable]
public sealed record SilverBalanceMessageComposer : IComposer
{
    [Id(0)]
    public required int SilverBalance { get; init; }
}
