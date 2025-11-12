using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record ObjectsDataUpdateMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
