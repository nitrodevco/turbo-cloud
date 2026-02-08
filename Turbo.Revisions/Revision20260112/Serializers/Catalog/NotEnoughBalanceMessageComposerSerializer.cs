using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Catalog;

internal class NotEnoughBalanceMessageComposerSerializer(int header)
    : AbstractSerializer<NotEnoughBalanceMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, NotEnoughBalanceMessageComposer message)
    {
        packet.WriteBoolean(message.NotEnoughCredits);
        packet.WriteBoolean(message.NotEnoughActivityPoints);
        packet.WriteInteger(message.ActivityPointType);
    }
}
