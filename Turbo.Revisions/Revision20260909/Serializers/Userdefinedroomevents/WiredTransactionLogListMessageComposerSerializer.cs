using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

internal class WiredTransactionLogListMessageComposerSerializer(int header)
    : AbstractSerializer<WiredTransactionLogListMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredTransactionLogListMessageComposer message
    )
    {
        WiredTransactionLogListSerializer.Serialize(packet, message.LogList);
    }
}
