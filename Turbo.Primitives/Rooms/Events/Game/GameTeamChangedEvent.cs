using Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Events.Game;

/// <summary>
/// An avatar (a player or a bot) joined or left a game team; <see cref="Team"/> is where it is now.
/// </summary>
[GenerateSerializer]
public sealed record GameTeamChangedEvent : RoomEvent
{
    /// <summary>The player; none for a bot.</summary>
    [Id(0)]
    public PlayerId PlayerId { get; init; }

    [Id(1)]
    public required GameTeamType Team { get; init; }

    [Id(2)]
    public required RoomObjectId AvatarId { get; init; }
}
