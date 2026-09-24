using System.Collections.Immutable;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Users.Data;

internal class GuildRoomOptionSerializer
{
    public static void Serialize(
        IServerPacket packet,
        ImmutableArray<GuildRoomOptionSnapshot> rooms
    )
    {
        packet.WriteInteger(rooms.Length);

        foreach (var room in rooms)
            packet
                .WriteInteger(room.RoomId)
                .WriteString(room.Name)
                .WriteBoolean(room.HasControllers);
    }
}
