using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Catalog;

internal class RoomAdPurchaseInfoEventMessageComposerSerializer(int header)
    : AbstractSerializer<RoomAdPurchaseInfoEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        RoomAdPurchaseInfoEventMessageComposer message
    )
    {
        //
    }
}
