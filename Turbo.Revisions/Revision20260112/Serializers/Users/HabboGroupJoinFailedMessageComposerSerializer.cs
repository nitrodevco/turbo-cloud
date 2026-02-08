using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class HabboGroupJoinFailedMessageComposerSerializer(int header)
    : AbstractSerializer<HabboGroupJoinFailedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        HabboGroupJoinFailedMessageComposer message
    )
    {
        //
    }
}
