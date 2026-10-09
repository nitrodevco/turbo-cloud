using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Catalog;

internal class VoucherRedeemOkMessageComposerSerializer(int header)
    : AbstractSerializer<VoucherRedeemOkMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, VoucherRedeemOkMessageComposer message)
    {
        // The client reads the description first.
        packet.WriteString(message.ProductDescription);
        packet.WriteString(message.ProductName);
    }
}
