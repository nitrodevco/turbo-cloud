using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Help;

[GenerateSerializer, Immutable]
public sealed record ChatReviewSessionStartedMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
