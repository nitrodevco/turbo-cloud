using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Orleans.Snapshots.Room.Furniture;

namespace Turbo.Primitives.Messages.Outgoing.Room.Furniture;

[GenerateSerializer, Immutable]
public sealed record AreaHideMessageComposer : IComposer
{
    [Id(0)]
    public required AreaHideDataSnapshot AreaHideData { get; init; }
}
