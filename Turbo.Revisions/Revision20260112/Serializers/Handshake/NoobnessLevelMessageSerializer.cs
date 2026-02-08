using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Handshake;

internal class NoobnessLevelMessageSerializer(int header)
    : AbstractSerializer<NoobnessLevelMessage>(header)
{
    protected override void Serialize(IServerPacket packet, NoobnessLevelMessage message)
    {
        packet.WriteInteger((int)message.NoobnessLevel);
    }
}
