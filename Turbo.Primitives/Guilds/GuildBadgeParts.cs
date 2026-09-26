using System.Collections.Immutable;
using System.Linq;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Snapshots;

namespace Turbo.Primitives.Guilds;

/// <summary>
/// Deciding what a badge is actually made of, given what a client sent and what the editor was
/// offered. Creating a group and redrawing its badge both end here, so the two cannot come to
/// different answers about the same request.
/// </summary>
public static class GuildBadgeParts
{
    /// <summary>
    /// Keeps only parts and colours the editor was actually offered, one base and four symbols
    /// at most, in layer order. A client is free to send anything; this is what decides what a
    /// badge is made of.
    /// </summary>
    public static ImmutableArray<GuildBadgePartSnapshot> Sanitize(
        ImmutableArray<GuildBadgePartSnapshot> parts,
        GuildEditorDataSnapshot editorData
    )
    {
        var baseIds = editorData.BaseParts.Select(x => x.PartId).ToHashSet();
        var symbolIds = editorData.SymbolParts.Select(x => x.PartId).ToHashSet();
        var colorIds = editorData.BadgeColors.Select(x => x.ColorId).ToHashSet();

        return
        [
            .. parts
                .Where(part =>
                    colorIds.Contains(part.ColorId)
                    && part.Position >= 0
                    && part.Position <= GuildBadgeCodes.POSITION_MAX
                    && (
                        part.Type == GuildBadgePartType.Base
                            ? baseIds.Contains(part.PartId)
                            : symbolIds.Contains(part.PartId)
                    )
                    && FitsInCode(part)
                )
                .Take(GuildBadgeCodes.MAX_PARTS),
        ];
    }

    /// <summary>
    /// Whether this part can be written into a badge code at all. Every field in a code is
    /// fixed width, so an id past its limit would run over into the next one and the whole code
    /// would read back as a different badge.
    ///
    /// Nothing in the hotel's own seed goes near these, but the parts and the palettes are rows
    /// an operator can add to, and a part that cannot be written is better dropped here than
    /// quietly corrupting every badge that uses it.
    /// </summary>
    private static bool FitsInCode(GuildBadgePartSnapshot part) =>
        part.ColorId >= 0
        && part.ColorId <= GuildBadgeCodes.COLOR_KEY_MAX
        && part.PartId >= 0
        && part.PartId
            <= (
                part.Type == GuildBadgePartType.Base
                    ? GuildBadgeCodes.BASE_KEY_MAX
                    : GuildBadgeCodes.SYMBOL_KEY_MAX
            );

    /// <summary>
    /// The chosen colour id, or the palette's first when the client sent one that is not in it.
    /// Its own editor cannot produce that, and refusing would strand a wizard whose only fault
    /// is a colour; a group in the wrong shade can be recoloured, an unmade group cannot.
    /// </summary>
    public static int PickColorId(ImmutableArray<GuildColorSnapshot> palette, int colorId) =>
        palette.Any(x => x.ColorId == colorId) ? colorId : palette.FirstOrDefault()?.ColorId ?? 0;
}
