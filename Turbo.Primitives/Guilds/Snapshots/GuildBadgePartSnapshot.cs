using Orleans;
using Turbo.Primitives.Guilds.Enums;

namespace Turbo.Primitives.Guilds.Snapshots;

/// <summary>
/// One layer of a badge: which part, tinted which colour, in which of the nine grid cells. The
/// client sends these as a flat run of three ints per layer and reads them back the same way,
/// and <see cref="GuildBadgeCodes"/> turns a list of them into the badge code.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GuildBadgePartSnapshot
{
    /// <summary>
    /// Not on the wire: the client knows a base from a symbol by which layer it is in. It is
    /// here because the code builder cannot tell them apart from the ids alone.
    /// </summary>
    [Id(0)]
    public required GuildBadgePartType Type { get; init; }

    /// <summary>The part's id in <c>guild_badge_parts</c>. Zero is an empty layer.</summary>
    [Id(1)]
    public required int PartId { get; init; }

    [Id(2)]
    public required int ColorId { get; init; }

    /// <summary>A cell of the 3x3 grid, <c>0</c> to <see cref="GuildBadgeCodes.POSITION_MAX"/>.</summary>
    [Id(3)]
    public required int Position { get; init; }
}
