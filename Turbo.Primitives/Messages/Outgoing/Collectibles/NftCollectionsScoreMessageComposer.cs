using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Collectibles;

/// <summary>A wallet's collector score (Flash parser collectibles/NftCollectionsScoreMessageEventParser.as:18): score, highest score, level.</summary>
[GenerateSerializer, Immutable]
public sealed record NftCollectionsScoreMessageComposer : IComposer
{
    [Id(0)]
    public required int Score { get; init; }

    [Id(1)]
    public required int HighestScore { get; init; }

    [Id(2)]
    public required int Level { get; init; }
}
