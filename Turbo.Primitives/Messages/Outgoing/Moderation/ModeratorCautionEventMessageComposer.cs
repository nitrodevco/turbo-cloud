using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Moderation;

[GenerateSerializer, Immutable]
public sealed record ModeratorCautionEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
