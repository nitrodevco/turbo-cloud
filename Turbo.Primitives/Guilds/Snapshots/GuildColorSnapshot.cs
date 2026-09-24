using Orleans;
using Turbo.Primitives.Guilds.Enums;

namespace Turbo.Primitives.Guilds.Snapshots;

/// <summary>
/// One colour the badge editor may pick, from one of its three palettes. The client parses
/// <see cref="Color"/> as hexadecimal, so it travels as six hex digits with no leading hash.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GuildColorSnapshot
{
    [Id(0)]
    public required GuildColorSlotType Slot { get; init; }

    [Id(1)]
    public required int ColorId { get; init; }

    /// <summary>Six hex digits, no <c>#</c>.</summary>
    [Id(2)]
    public required string Color { get; init; }
}
