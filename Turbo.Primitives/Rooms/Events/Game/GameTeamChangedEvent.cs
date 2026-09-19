using Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Events.Game;

/// <summary>A player joined or left a game team; <see cref="Team"/> is where they are now.</summary>
[GenerateSerializer]
public sealed record GameTeamChangedEvent : RoomEvent
{
    [Id(0)]
    public required PlayerId PlayerId { get; init; }

    [Id(1)]
    public required GameTeamType Team { get; init; }
}
