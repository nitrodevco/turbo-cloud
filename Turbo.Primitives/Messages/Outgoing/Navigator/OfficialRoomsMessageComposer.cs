using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

/// <summary>The legacy official rooms list; each room is sent as a guest-room entry.</summary>
[GenerateSerializer, Immutable]
public sealed record OfficialRoomsMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<RoomInfoSnapshot> Rooms { get; init; }
}
