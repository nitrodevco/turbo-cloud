using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Handshake;

internal class GenericErrorMessageSerializer(int header)
    : AbstractSerializer<GenericErrorMessage>(header)
{
    protected override void Serialize(IServerPacket packet, GenericErrorMessage message)
    {
        packet.WriteInteger((int)message.ErrorCode);
    }
}
