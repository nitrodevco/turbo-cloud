using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Users.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Users;

internal class GroupMembershipRequestedMessageComposerSerializer(int header)
    : AbstractSerializer<GroupMembershipRequestedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GroupMembershipRequestedMessageComposer message
    )
    {
        packet.WriteInteger(message.GuildId);

        GuildMemberSerializer.Serialize(packet, message.Member);
    }
}
