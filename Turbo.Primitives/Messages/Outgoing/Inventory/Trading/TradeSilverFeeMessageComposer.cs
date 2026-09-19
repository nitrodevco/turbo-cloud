using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Trading;

/// <summary>Silver a collectible (NFT) trade costs; the client shows a fee bar above zero.</summary>
[GenerateSerializer, Immutable]
public sealed record TradeSilverFeeMessageComposer : IComposer
{
    [Id(0)]
    public required int SilverFee { get; init; }
}
