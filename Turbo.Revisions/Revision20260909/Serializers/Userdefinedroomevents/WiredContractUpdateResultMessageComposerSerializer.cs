using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

internal class WiredContractUpdateResultMessageComposerSerializer(int header)
    : AbstractSerializer<WiredContractUpdateResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredContractUpdateResultMessageComposer message
    )
    {
        packet
            .WriteInteger(message.ContractId)
            .WriteBoolean(message.IsSuccess)
            .WriteString(message.FailCode);
    }
}
