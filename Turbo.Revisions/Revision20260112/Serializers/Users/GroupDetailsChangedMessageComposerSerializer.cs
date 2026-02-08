using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class GroupDetailsChangedMessageComposerSerializer(int header)
    : AbstractSerializer<GroupDetailsChangedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GroupDetailsChangedMessageComposer message
    )
    {
        //
    }
}
