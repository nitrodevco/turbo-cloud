using Turbo.Primitives.Messages.Outgoing.Collectibles;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Collectibles;

internal class LtdRaffleEnteredMessageComposerSerializer(int header)
    : AbstractSerializer<LtdRaffleEnteredMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, LtdRaffleEnteredMessageComposer message)
    {
        packet.WriteString(message.ClassName);
    }
}
