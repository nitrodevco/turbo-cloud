using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

internal class WiredTradeTransactionNotificationMessageComposerSerializer(int header)
    : AbstractSerializer<WiredTradeTransactionNotificationMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredTradeTransactionNotificationMessageComposer message
    )
    {
        packet.WriteInteger((int)message.Error);
    }
}
