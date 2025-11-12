using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Action;

[GenerateSerializer, Immutable]
public sealed record DanceMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
