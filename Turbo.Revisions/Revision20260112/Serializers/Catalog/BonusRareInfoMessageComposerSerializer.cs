using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Catalog;

internal class BonusRareInfoMessageComposerSerializer(int header)
    : AbstractSerializer<BonusRareInfoMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, BonusRareInfoMessageComposer message)
    {
        packet.WriteString(message.ProductType);
        packet.WriteInteger(message.ProductClassId);
        packet.WriteInteger(message.TotalCoinsForBonus);
        packet.WriteInteger(message.CoinsStillRequiredToBuy);
    }
}
