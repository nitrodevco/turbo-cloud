using Turbo.Primitives.Messages.Outgoing.Collectibles;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Collectibles;

internal class EmeraldBalanceMessageComposerSerializer(int header)
    : AbstractSerializer<EmeraldBalanceMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, EmeraldBalanceMessageComposer message)
    {
        packet.WriteInteger(message.EmeraldBalance);
    }
}
