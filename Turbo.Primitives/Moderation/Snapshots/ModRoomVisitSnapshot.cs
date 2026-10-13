using Orleans;

namespace Turbo.Primitives.Moderation.Snapshots;

/// <summary>A room a player entered, as the room visits window lists it (hour and minute).</summary>
[GenerateSerializer, Immutable]
public sealed record ModRoomVisitSnapshot
{
    [Id(0)]
    public required int RoomId { get; init; }

    [Id(1)]
    public required string RoomName { get; init; }

    [Id(2)]
    public required int EnterHour { get; init; }

    [Id(3)]
    public required int EnterMinute { get; init; }
}
