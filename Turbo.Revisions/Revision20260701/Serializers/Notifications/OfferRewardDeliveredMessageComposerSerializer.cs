using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Notifications;

internal class OfferRewardDeliveredMessageComposerSerializer(int header)
    : AbstractSerializer<OfferRewardDeliveredMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        OfferRewardDeliveredMessageComposer message
    )
    {
        //
    }
}
