using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record ObjectAddMessageComposer : IComposer
{
    [Id(0)]
    public required RoomFloorItemSnapshot FloorItem { get; init; }
}
