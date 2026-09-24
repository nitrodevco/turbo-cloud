using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Guilds.Snapshots;

/// <summary>
/// What the create-a-group wizard opens on: the price, the rooms that could be the homeroom,
/// and the badge it starts the editor with.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GuildCreationInfoSnapshot
{
    [Id(0)]
    public required int CostInCredits { get; init; }

    /// <summary>
    /// Rooms this player owns that are not already somebody's homeroom. A room can only ever be
    /// one group's, so offering one that is taken would only be refused later.
    /// </summary>
    [Id(1)]
    public required ImmutableArray<GuildRoomOptionSnapshot> OwnedRooms { get; init; }

    /// <summary>The badge the editor starts on, which the player then changes.</summary>
    [Id(2)]
    public required ImmutableArray<GuildBadgePartSnapshot> BadgeParts { get; init; }
}
