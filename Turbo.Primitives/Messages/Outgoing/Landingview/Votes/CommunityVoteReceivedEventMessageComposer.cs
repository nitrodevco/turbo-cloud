using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Landingview.Votes;

/// <summary>Whether a vote for a side of the community goal counted: false for a second vote.</summary>
[GenerateSerializer, Immutable]
public sealed record CommunityVoteReceivedEventMessageComposer : IComposer
{
    [Id(0)]
    public required bool Acknowledged { get; init; }
}
