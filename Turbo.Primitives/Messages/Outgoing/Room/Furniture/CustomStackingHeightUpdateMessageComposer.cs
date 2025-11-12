using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Furniture;

[GenerateSerializer, Immutable]
public sealed record CustomStackingHeightUpdateMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
