using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Landingview.Votes;

[GenerateSerializer, Immutable]
public sealed record CommunityVoteReceivedEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
