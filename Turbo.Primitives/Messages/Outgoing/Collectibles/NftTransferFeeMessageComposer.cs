using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Collectibles;

/// <summary>The silver fee for transferring NFTs (Flash parser collectibles/NftTransferFeeMessageEventParser.as:17).</summary>
[GenerateSerializer, Immutable]
public sealed record NftTransferFeeMessageComposer : IComposer
{
    [Id(0)]
    public required int TransferFee { get; init; }
}
