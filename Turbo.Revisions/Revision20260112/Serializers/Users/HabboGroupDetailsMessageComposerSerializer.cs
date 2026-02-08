using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class HabboGroupDetailsMessageComposerSerializer(int header)
    : AbstractSerializer<HabboGroupDetailsMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        HabboGroupDetailsMessageComposer message
    )
    {
        //
    }
}
