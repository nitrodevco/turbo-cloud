using Turbo.Primitives.Messages.Outgoing.Perk;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Perk;

internal class CitizenshipVipOfferPromoEnabledEventMessageComposerSerializer(int header)
    : AbstractSerializer<CitizenshipVipOfferPromoEnabledEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CitizenshipVipOfferPromoEnabledEventMessageComposer message
    )
    {
        //
    }
}
