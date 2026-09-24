using System.Collections.Immutable;
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
    public static ImmutableArray<GuildBadgePartSnapshot> Parse(IClientPacket packet)
    {
        var count = packet.PopInt();

        if (count <= 0)
            return [];

        // A client is free to claim any count. Only as many layers as a badge can hold are kept,
        // but every layer it claimed is read, or the rest of the packet would be misaligned.
        var parts = ImmutableArray.CreateBuilder<GuildBadgePartSnapshot>();

        for (var layer = 0; layer < count; layer++)
        {
            var part = new GuildBadgePartSnapshot
            {
                Type = layer == 0 ? GuildBadgePartType.Base : GuildBadgePartType.Symbol,
                PartId = packet.PopInt(),
                ColorId = packet.PopInt(),
                Position = packet.PopInt(),
            };

            if (layer < GuildBadgeCodes.MAX_PARTS)
                parts.Add(part);
        }

        return parts.ToImmutable();
    }
}
