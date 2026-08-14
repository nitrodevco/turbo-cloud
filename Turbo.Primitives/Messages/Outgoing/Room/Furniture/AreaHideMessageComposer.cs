using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Primitives.Messages.Outgoing.Room.Furniture;

[GenerateSerializer, Immutable]
public sealed record AreaHideMessageComposer : IComposer
{
    [Id(0)]
    public required AreaHideDataSnapshot AreaHideData { get; init; }
}
