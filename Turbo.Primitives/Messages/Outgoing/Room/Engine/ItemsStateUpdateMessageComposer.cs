using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record ItemsStateUpdateMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
