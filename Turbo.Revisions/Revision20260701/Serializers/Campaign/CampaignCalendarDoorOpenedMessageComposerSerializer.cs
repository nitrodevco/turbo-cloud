using Turbo.Primitives.Messages.Outgoing.Campaign;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Campaign;

internal class CampaignCalendarDoorOpenedMessageComposerSerializer(int header)
    : AbstractSerializer<CampaignCalendarDoorOpenedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CampaignCalendarDoorOpenedMessageComposer message
    )
    {
        //
    }
}
