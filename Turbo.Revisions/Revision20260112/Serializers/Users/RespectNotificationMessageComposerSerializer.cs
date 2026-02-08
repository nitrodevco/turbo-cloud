using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class RespectNotificationMessageComposerSerializer(int header)
    : AbstractSerializer<RespectNotificationMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        RespectNotificationMessageComposer message
    )
    {
        //
    }
}
