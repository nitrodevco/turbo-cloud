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

    /// <summary>
    /// Whether the clock counted its way here; false when Adjust Clock or a reset put it there,
    /// which Clock Reaches Time does not fire for.
    /// </summary>
    [Id(2)]
    public bool IsCounted { get; init; } = true;
}
