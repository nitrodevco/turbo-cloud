using Orleans;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Guilds.Snapshots;

/// <summary>
/// What every part of the hotel needs to know about a group without loading it: enough to draw
/// its badge, name it, and find its homeroom. This is what the guild directory keeps in memory
/// for every group, so it stays to fields that are cheap to hold and rarely change.
/// </summary>
[GenerateSerializer, Immutable]
public record GuildSummarySnapshot
{
    [Id(0)]
    public required GuildId GuildId { get; init; } = GuildId.Invalid;

    [Id(1)]
    public required string Name { get; init; } = string.Empty;

    [Id(2)]
    public required string BadgeCode { get; init; } = string.Empty;

    /// <summary>The homeroom. Chosen once at creation and never changed.</summary>
    [Id(3)]
    public required RoomId RoomId { get; init; } = -1;

    [Id(4)]
    public required PlayerId OwnerId { get; init; } = -1;

    /// <summary>
    /// The colours as ids into the badge editor's palettes. The edit window preselects by id,
    /// so the id is what the group stores; the hex below is looked up from it.
    /// </summary>
    [Id(5)]
    public required int PrimaryColorId { get; init; }

    [Id(6)]
    public required int SecondaryColorId { get; init; }

    /// <summary>Six hex digits, no <c>#</c>; what this group's furni recolours to.</summary>
    [Id(7)]
    public required string PrimaryColor { get; init; } = string.Empty;

    [Id(8)]
    public required string SecondaryColor { get; init; } = string.Empty;

    [Id(9)]
    public required GuildType Type { get; init; } = GuildType.Regular;

    /// <summary>
    /// Whether the group has a forum. False for every group until the forum ship lands, and
    /// honestly false rather than stubbed: the client draws no forum link for it.
    /// </summary>
    [Id(10)]
    public required bool HasForum { get; init; }
}
