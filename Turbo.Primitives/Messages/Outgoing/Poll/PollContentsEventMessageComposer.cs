using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Poll;

[GenerateSerializer, Immutable]
public sealed record PollContentsEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
