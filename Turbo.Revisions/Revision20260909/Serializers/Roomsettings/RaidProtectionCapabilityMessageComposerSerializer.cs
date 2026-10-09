using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Roomsettings;

internal class RaidProtectionCapabilityMessageComposerSerializer(int header)
    : AbstractSerializer<RaidProtectionCapabilityMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        RaidProtectionCapabilityMessageComposer message
    )
    {
        packet.WriteInteger(message.RoomId).WriteBoolean(message.CanManage);
    }
}
