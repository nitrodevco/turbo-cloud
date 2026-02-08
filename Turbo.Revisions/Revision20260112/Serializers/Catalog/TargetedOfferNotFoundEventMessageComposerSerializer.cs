using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Catalog;

internal class TargetedOfferNotFoundEventMessageComposerSerializer(int header)
    : AbstractSerializer<TargetedOfferNotFoundEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        TargetedOfferNotFoundEventMessageComposer message
    )
    {
        //
    }
}
