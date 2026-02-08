using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Handshake;

internal class PingMessageSerializer(int header) : AbstractSerializer<PingMessage>(header)
{
    protected override void Serialize(IServerPacket packet, PingMessage message)
    {
        //
    }
}
