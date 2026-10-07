using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

internal class WiredTransactionSuccessMessageComposerSerializer(int header)
    : AbstractSerializer<WiredTransactionSuccessMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredTransactionSuccessMessageComposer message
    )
    {
        WiredTransactionSuccessContentsSerializer.Serialize(packet, message.Contents);
    }
}
