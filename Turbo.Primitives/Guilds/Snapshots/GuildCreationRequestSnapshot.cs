using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Guilds.Snapshots;

/// <summary>
/// What the create wizard sends when it commits. It is the client's word for all of it — the
/// name and description are clamped and the ids are checked against what the editor was offered
/// before any of it becomes a group.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GuildCreationRequestSnapshot
{
    [Id(0)]
    public required string Name { get; init; }

    [Id(1)]
    public required string Description { get; init; }

    /// <summary>The room the creator picked as the homeroom. Never changes afterwards.</summary>
    [Id(2)]
    public required RoomId RoomId { get; init; }

    [Id(3)]
    public required int PrimaryColorId { get; init; }

    [Id(4)]
    public required int SecondaryColorId { get; init; }

    /// <summary>One base and up to four symbols, in layer order.</summary>
    [Id(5)]
    public required ImmutableArray<GuildBadgePartSnapshot> BadgeParts { get; init; }
}
