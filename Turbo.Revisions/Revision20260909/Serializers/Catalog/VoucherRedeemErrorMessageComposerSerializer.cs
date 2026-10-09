using System.Globalization;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Catalog;

internal class VoucherRedeemErrorMessageComposerSerializer(int header)
    : AbstractSerializer<VoucherRedeemErrorMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        VoucherRedeemErrorMessageComposer message
    )
    {
        // A string: the client puts it at the end of its text key.
        packet.WriteString(((int)message.Error).ToString(CultureInfo.InvariantCulture));
    }
}
