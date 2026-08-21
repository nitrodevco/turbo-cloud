using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260701.Serializers.Catalog.Data;

namespace Turbo.Revisions.Revision20260701.Serializers.Catalog;

internal class BundleDiscountRulesetMessageComposerSerializer(int header)
    : AbstractSerializer<BundleDiscountRulesetMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        BundleDiscountRulesetMessageComposer message
    )
    {
        BundleDiscountRulesetSnapshotSerializer.Serialize(packet, message.BundleDiscountRuleset);
    }
}
