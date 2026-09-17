using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Catalog.Data;

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
