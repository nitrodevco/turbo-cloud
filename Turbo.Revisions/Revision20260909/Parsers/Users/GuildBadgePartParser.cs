using System.Collections.Immutable;
using System.Linq;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Users;

/// <summary>
/// The badge the editor sends: a count, then part, colour and position per layer, run together
/// in one flat array. The client does not say which layer is the base — the first one is, and
/// the rest are symbols, which is how it draws them.
/// </summary>
internal static class GuildBadgePartParser
{
    /// <summary>Part, colour and position: three ints a layer.</summary>
    private const int BYTES_PER_LAYER = 12;

    public static ImmutableArray<GuildBadgePartSnapshot> Parse(IClientPacket packet)
    {
        // A client is free to claim any count; PopList bounds it by what the packet holds. Only
        // as many layers as a badge can hold are kept, but every layer it claimed is read (no
        // maxItems here), or the rest of the packet would be misaligned.
        var layers = packet.PopList(
            BYTES_PER_LAYER,
            static p => (PartId: p.PopInt(), ColorId: p.PopInt(), Position: p.PopInt())
        );

        return
        [
            .. layers
                .Take(GuildBadgeCodes.MAX_PARTS)
                .Select(
                    (layer, index) =>
                        new GuildBadgePartSnapshot
                        {
                            Type = index == 0 ? GuildBadgePartType.Base : GuildBadgePartType.Symbol,
                            PartId = layer.PartId,
                            ColorId = layer.ColorId,
                            Position = layer.Position,
                        }
                ),
        ];
    }
}
