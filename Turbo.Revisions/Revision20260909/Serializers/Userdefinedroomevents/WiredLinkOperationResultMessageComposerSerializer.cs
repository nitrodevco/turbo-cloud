using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

internal class WiredLinkOperationResultMessageComposerSerializer(int header)
    : AbstractSerializer<WiredLinkOperationResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredLinkOperationResultMessageComposer message
    )
    {
        packet
            .WriteInteger(message.RoomId)
            .WriteInteger(message.FurniId)
            .WriteInteger(message.Operation)
            .WriteInteger(message.ResultCode)
            .WriteBoolean(message.SelectedNetworkId is not null);

        if (message.SelectedNetworkId is not null)
            packet.WriteString(message.SelectedNetworkId).WriteBoolean(message.ReceiveLocal);

        packet.WriteString(message.RemovedNetworkId).WriteInteger(message.Networks.Length);

        foreach (var network in message.Networks)
        {
            packet
                .WriteString(network.NetworkId)
                .WriteString(network.Name)
                .WriteInteger(network.ConnectionCount)
                .WriteBoolean(network.LinkedInCurrentRoom);
        }
    }
}
