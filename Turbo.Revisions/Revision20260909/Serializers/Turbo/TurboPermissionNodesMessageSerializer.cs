using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Turbo;

internal class TurboPermissionNodesMessageSerializer(int header)
    : AbstractSerializer<TurboPermissionNodesMessage>(header)
{
    protected override void Serialize(IServerPacket packet, TurboPermissionNodesMessage message)
    {
        packet.WriteInteger(message.Nodes.Length);

        foreach (var node in message.Nodes)
            packet.WriteString(node);
    }
}
