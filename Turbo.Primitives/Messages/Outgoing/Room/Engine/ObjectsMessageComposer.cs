using System.Collections.Generic;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Orleans.Snapshots.Rooms;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

public sealed record ObjectsMessageComposer : IComposer
{
    public required Dictionary<int, string> OwnerNames { get; init; }
    public required IReadOnlyList<RoomFloorItemSnapshot> FloorItems { get; init; }
}
