using Orleans;
using Turbo.Primitives.Guilds.Enums;

namespace Turbo.Primitives.Guilds.Snapshots;

/// <summary>
/// A part the badge editor may pick, and the two assets that draw it. The client loads them as
/// <c>badgepart_&lt;name&gt;.png</c> from its part library, tints the first with the layer's
/// colour and lays the second over it untinted; a part that is one flat shape leaves
/// <see cref="MaskFileName"/> empty.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GuildBadgePartDefinitionSnapshot
{
    [Id(0)]
    public required GuildBadgePartType Type { get; init; }

    [Id(1)]
    public required int PartId { get; init; }

    [Id(2)]
    public required string FileName { get; init; }

    /// <summary>Empty when the part has no untinted overlay.</summary>
    [Id(3)]
    public required string MaskFileName { get; init; }
}
