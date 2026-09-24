using System.Collections.Immutable;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Users.Data;

/// <summary>
/// A badge as the editor reads it: a count, then part, colour and position per layer. The same
/// three ints the client sends back when it saves.
/// </summary>
internal class GuildBadgePartSerializer
{
    public static void Serialize(IServerPacket packet, ImmutableArray<GuildBadgePartSnapshot> parts)
    {
        packet.WriteInteger(parts.Length);

        foreach (var part in parts)
            packet.WriteInteger(part.PartId).WriteInteger(part.ColorId).WriteInteger(part.Position);
    }
}
