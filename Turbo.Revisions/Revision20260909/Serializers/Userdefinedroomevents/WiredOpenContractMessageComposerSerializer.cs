using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

internal class WiredOpenContractMessageComposerSerializer(int header)
    : AbstractSerializer<WiredOpenContractMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredOpenContractMessageComposer message
    )
    {
        packet.WriteInteger(message.ContractId);
    }
}
