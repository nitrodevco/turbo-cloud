using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Poll;

/// <summary>A poll offered to the player (Flash parser poll/PollOfferParser.as:43).</summary>
[GenerateSerializer, Immutable]
public sealed record PollOfferEventMessageComposer : IComposer
{
    [Id(0)]
    public required int PollId { get; init; }

    [Id(1)]
    public required string PollType { get; init; }

    [Id(2)]
    public required string Headline { get; init; }

    [Id(3)]
    public required string Summary { get; init; }
}
