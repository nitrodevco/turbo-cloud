using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Help;

[GenerateSerializer, Immutable]
public sealed record ChatReviewSessionOfferedToGuideMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
