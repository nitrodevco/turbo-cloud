using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Catalog;

internal class TargetedOfferEventMessageComposerSerializer(int header)
    : AbstractSerializer<TargetedOfferEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        TargetedOfferEventMessageComposer message
    )
    {
        //
    }
}
