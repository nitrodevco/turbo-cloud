using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Orleans.Snapshots.Room;

namespace Turbo.Primitives.Orleans.States.Rooms;

[GenerateSerializer]
public sealed class RoomDirectoryState
{
    [Id(0)]
    public required Dictionary<long, RoomActiveInfoSnapshot> ActiveRooms { get; set; } = [];
}
