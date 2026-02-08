using Turbo.Primitives.Packets;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Revisions.Revision20260112.Serializers.Catalog.Data;

internal class BundleDiscountRulesetSnapshotSerializer
{
    public static void Serialize(IServerPacket packet, BundleDiscountRulesetSnapshot message)
    {
        packet
            .WriteInteger(message.MaxPurchaseSize)
            .WriteInteger(message.BundleSize)
            .WriteInteger(message.BundleDiscountSize)
            .WriteInteger(message.BonusThreshold)
            .WriteInteger(message.AdditionalBonusDiscountThresholdQuantities.Length);

        foreach (var quantity in message.AdditionalBonusDiscountThresholdQuantities)
        {
            packet.WriteInteger(quantity);
        }
    }
}
