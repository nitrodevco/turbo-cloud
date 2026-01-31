using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record ItemsStateUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required List<(RoomObjectId, string)> ObjectStates { get; init; }
}
