using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record WiredMovementsMessageComposer : IComposer
{
    [Id(0)]
    public required List<WiredUserMovementSnapshot> Users { get; init; }

    [Id(1)]
    public required List<WiredFloorItemMovementSnapshot> FloorItems { get; init; }

    [Id(2)]
    public required List<WiredWallItemMovementSnapshot> WallItems { get; init; }

    [Id(3)]
    public required List<WiredUserDirectionSnapshot> UserDirections { get; init; }
}
