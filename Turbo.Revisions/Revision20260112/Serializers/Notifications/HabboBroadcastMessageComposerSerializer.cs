using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Notifications;

internal class HabboBroadcastMessageComposerSerializer(int header)
    : AbstractSerializer<HabboBroadcastMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, HabboBroadcastMessageComposer message)
    {
        //
    }
}
