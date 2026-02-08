using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Catalog;

internal class PurchaseErrorMessageComposerSerializer(int header)
    : AbstractSerializer<PurchaseErrorMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, PurchaseErrorMessageComposer message)
    {
        packet.WriteInteger(message.ErrorCode);
    }
}
