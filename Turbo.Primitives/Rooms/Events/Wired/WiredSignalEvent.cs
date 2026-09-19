using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Events.Wired;

/// <summary>
/// A "send signal" action fired. <see cref="AntennaIds"/> are the furni the sender addressed;
/// every "receive signal" trigger whose own antenna selection meets them fires with the forwarded
/// furni and users as its signal payload.
/// </summary>
[GenerateSerializer]
public sealed record WiredSignalEvent : RoomEvent
{
    [Id(0)]
    public required HashSet<int> AntennaIds { get; init; }

    [Id(1)]
    public required HashSet<int> FurniIds { get; init; }

    [Id(2)]
    public required HashSet<int> PlayerIds { get; init; }

    [Id(3)]
    public required int Depth { get; init; }

    [Id(4)]
    public RoomObjectId SenderId { get; init; }
}
