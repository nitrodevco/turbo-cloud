using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

internal class WiredTransactionLogDetailsMessageComposerSerializer(int header)
    : AbstractSerializer<WiredTransactionLogDetailsMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredTransactionLogDetailsMessageComposer message
    )
    {
        WiredTransactionDetailsSerializer.Serialize(packet, message.Details);
    }
}
