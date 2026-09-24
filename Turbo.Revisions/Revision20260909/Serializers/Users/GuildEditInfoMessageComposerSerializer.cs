using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Users.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Users;

internal class GuildEditInfoMessageComposerSerializer(int header)
    : AbstractSerializer<GuildEditInfoMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, GuildEditInfoMessageComposer message)
    {
        var guild = message.Guild;

        GuildRoomOptionSerializer.Serialize(packet, message.OwnedRooms);

        packet
            .WriteBoolean(message.IsOwner)
            .WriteInteger(guild.GuildId)
            .WriteString(guild.Name)
            .WriteString(guild.Description)
            .WriteInteger(guild.RoomId)
            .WriteInteger(guild.PrimaryColorId)
            .WriteInteger(guild.SecondaryColorId)
            .WriteInteger((int)guild.Type)
            .WriteInteger((int)guild.RightsLevel)
            // The client greys the whole window out when this is set; nothing sets it here.
            .WriteBoolean(false)
            .WriteString(string.Empty);

        GuildBadgePartSerializer.Serialize(packet, message.BadgeParts);

        packet.WriteString(guild.BadgeCode).WriteInteger(message.MemberCount);
    }
}
