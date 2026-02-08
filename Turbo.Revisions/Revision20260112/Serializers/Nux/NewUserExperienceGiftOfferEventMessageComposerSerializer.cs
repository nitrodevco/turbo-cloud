using Turbo.Primitives.Messages.Outgoing.Nux;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Nux;

internal class NewUserExperienceGiftOfferEventMessageComposerSerializer(int header)
    : AbstractSerializer<NewUserExperienceGiftOfferEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        NewUserExperienceGiftOfferEventMessageComposer message
    )
    {
        //
    }
}
