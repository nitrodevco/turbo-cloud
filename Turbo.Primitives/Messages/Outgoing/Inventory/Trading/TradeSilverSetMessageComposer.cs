using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Trading;

/// <summary>How much of the silver fee each party has put in.</summary>
[GenerateSerializer, Immutable]
public sealed record TradeSilverSetMessageComposer : IComposer
{
    [Id(0)]
    public required int PlayerSilver { get; init; }

    [Id(1)]
    public required int OtherPlayerSilver { get; init; }
}
