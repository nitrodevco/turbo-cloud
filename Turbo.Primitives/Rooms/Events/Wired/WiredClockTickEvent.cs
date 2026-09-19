using Orleans;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Events.Wired;

/// <summary>A counter clock advanced. <see cref="HalfSeconds"/> is its elapsed time in pulses.</summary>
[GenerateSerializer]
public sealed record WiredClockTickEvent : RoomEvent
{
    [Id(0)]
    public required RoomObjectId ObjectId { get; init; }

    [Id(1)]
    public required int HalfSeconds { get; init; }
}
