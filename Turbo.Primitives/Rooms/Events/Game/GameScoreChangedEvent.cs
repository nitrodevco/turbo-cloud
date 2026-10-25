using Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

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

    /// <summary>
    /// The avatar (a player or a bot) Give Score scored for; none for a team score (Give Score To
    /// Team) or a set.
    /// </summary>
    [Id(3)]
    public RoomObjectId ScoredBy { get; init; }
}
