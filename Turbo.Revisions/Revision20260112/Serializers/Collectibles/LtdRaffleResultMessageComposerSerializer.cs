using Turbo.Primitives.Messages.Outgoing.Collectibles;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Collectibles;

internal class LtdRaffleResultMessageComposerSerializer(int header)
    : AbstractSerializer<LtdRaffleResultMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, LtdRaffleResultMessageComposer message)
    {
        packet.WriteString(message.ClassName);
        packet.WriteByte((byte)message.ResultCode);
    }
}
