using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

internal class WiredTradeCancelledMessageComposerSerializer(int header)
    : AbstractSerializer<WiredTradeCancelledMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredTradeCancelledMessageComposer message
    )
    {
        packet.WriteInteger((int)message.FailureType);
    }
}
