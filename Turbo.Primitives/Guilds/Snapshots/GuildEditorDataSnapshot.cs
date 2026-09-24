using System.Collections.Immutable;
using System.Linq;
using Orleans;
using Turbo.Primitives.Guilds.Enums;

namespace Turbo.Primitives.Guilds.Snapshots;

/// <summary>
/// Everything the badge editor may pick from. Hotel data, the same for every player, and the
/// client asks for it once and keeps it — so this is loaded with the guild directory rather
/// than read per request.
///
/// The client has no built-in list of any of this: an id it is not told about here is an id it
/// cannot draw, which is why the same table feeds both this and the badge code.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GuildEditorDataSnapshot
{
    [Id(0)]
    public required ImmutableArray<GuildBadgePartDefinitionSnapshot> BaseParts { get; init; }

    [Id(1)]
    public required ImmutableArray<GuildBadgePartDefinitionSnapshot> SymbolParts { get; init; }

    /// <summary>What a badge layer may be tinted with.</summary>
    [Id(2)]
    public required ImmutableArray<GuildColorSnapshot> BadgeColors { get; init; }

    [Id(3)]
    public required ImmutableArray<GuildColorSnapshot> PrimaryColors { get; init; }

    [Id(4)]
    public required ImmutableArray<GuildColorSnapshot> SecondaryColors { get; init; }

    /// <summary>
    /// The hex behind a colour id. A group stores the id, so this is what turns one back into
    /// something the client can paint with; an id no longer in the palette falls back to the
    /// palette's first colour rather than to nothing, because a group with no colour draws no
    /// furni at all.
    /// </summary>
    public string GetColor(GuildColorSlotType slot, int colorId)
    {
        var palette = slot switch
        {
            GuildColorSlotType.Primary => PrimaryColors,
            GuildColorSlotType.Secondary => SecondaryColors,
            _ => BadgeColors,
        };

        return palette.FirstOrDefault(x => x.ColorId == colorId)?.Color
            ?? palette.FirstOrDefault()?.Color
            ?? string.Empty;
    }
}
