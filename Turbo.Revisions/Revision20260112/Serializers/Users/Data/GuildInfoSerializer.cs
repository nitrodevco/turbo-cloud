using Turbo.Primitives.Groups.Snapshots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users.Data;

internal class GuildInfoSerializer
{
    public static void Serialize(IServerPacket packet, GuildInfoSnapshot guild)
    {
        packet
            .WriteInteger(guild.GroupId)
            .WriteString(guild.GroupName)
            .WriteString(guild.BadgeCode)
            .WriteString(guild.PrimaryColor)
            .WriteString(guild.SecondaryColor)
            .WriteBoolean(guild.Favourite)
            .WriteInteger(guild.OwnerId)
            .WriteBoolean(guild.HasForum);
    }
}
