using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record ObjectsDataUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required List<(RoomObjectId, StuffDataSnapshot)> StuffDatas { get; init; }
}
