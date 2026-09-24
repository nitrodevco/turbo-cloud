using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Users.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Users;

internal class GuildCreationInfoMessageComposerSerializer(int header)
    : AbstractSerializer<GuildCreationInfoMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GuildCreationInfoMessageComposer message
    )
    {
        var info = message.CreationInfo;

        packet.WriteInteger(info.CostInCredits);

        GuildRoomOptionSerializer.Serialize(packet, info.OwnedRooms);
        GuildBadgePartSerializer.Serialize(packet, info.BadgeParts);
    }
}
