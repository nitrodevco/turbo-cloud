using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class HabboGroupDeactivatedMessageComposerSerializer(int header)
    : AbstractSerializer<HabboGroupDeactivatedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        HabboGroupDeactivatedMessageComposer message
    )
    {
        //
    }
}
