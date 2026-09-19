using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Events.Game;

/// <summary>A team score moved; <see cref="Score"/> is the new total.</summary>
[GenerateSerializer]
public sealed record GameScoreChangedEvent : RoomEvent
{
    [Id(0)]
    public required GameTeamType Team { get; init; }

    [Id(1)]
    public required int Score { get; init; }

    [Id(2)]
    public required int PreviousScore { get; init; }
}
