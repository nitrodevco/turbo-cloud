using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

internal class WiredTransactionFailMessageComposerSerializer(int header)
    : AbstractSerializer<WiredTransactionFailMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredTransactionFailMessageComposer message
    )
    {
        packet.WriteInteger((int)message.FailureType);
    }
}
