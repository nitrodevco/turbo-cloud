using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Catalog;

internal class GiftReceiverNotFoundEventMessageComposerSerializer(int header)
    : AbstractSerializer<GiftReceiverNotFoundEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GiftReceiverNotFoundEventMessageComposer message
    )
    {
        //
    }
}
