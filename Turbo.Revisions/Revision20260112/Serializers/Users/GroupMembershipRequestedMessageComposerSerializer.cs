using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class GroupMembershipRequestedMessageComposerSerializer(int header)
    : AbstractSerializer<GroupMembershipRequestedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GroupMembershipRequestedMessageComposer message
    )
    {
        //
    }
}
