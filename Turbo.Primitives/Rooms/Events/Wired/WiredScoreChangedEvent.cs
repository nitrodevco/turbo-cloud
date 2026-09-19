using Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Rooms.Events.Wired;

/// <summary>A team score moved; <see cref="Score"/> is the new total.</summary>
[GenerateSerializer]
public sealed record WiredScoreChangedEvent : RoomEvent
{
    [Id(0)]
    public required WiredTeamType Team { get; init; }

    [Id(1)]
    public required int Score { get; init; }

    [Id(2)]
    public required int PreviousScore { get; init; }
}
