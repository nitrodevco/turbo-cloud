using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

internal class WiredLinkConfigurationMessageComposerSerializer(int header)
    : AbstractSerializer<WiredLinkConfigurationMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredLinkConfigurationMessageComposer message
    )
    {
        packet
            .WriteInteger(message.RoomId)
            .WriteInteger(message.FurniId)
            .WriteString(message.SelectedNetworkId)
            .WriteBoolean(message.ReceiveLocal)
            .WriteInteger(message.Networks.Length);

        foreach (var network in message.Networks)
            WiredLinkOperationResultMessageComposerSerializer.WriteNetwork(packet, network);
    }
}
