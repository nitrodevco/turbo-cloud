using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Users.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Users;

internal class GuildMembershipUpdatedMessageComposerSerializer(int header)
    : AbstractSerializer<GuildMembershipUpdatedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GuildMembershipUpdatedMessageComposer message
    )
    {
        packet.WriteInteger(message.GuildId);

        GuildMemberSerializer.Serialize(packet, message.Member);
    }
}
