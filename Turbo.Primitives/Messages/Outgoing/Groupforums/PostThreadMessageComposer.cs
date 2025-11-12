using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Groupforums;

[GenerateSerializer, Immutable]
public sealed record PostThreadMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
