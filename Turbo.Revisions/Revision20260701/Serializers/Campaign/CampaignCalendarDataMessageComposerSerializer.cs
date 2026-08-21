using Turbo.Primitives.Messages.Outgoing.Campaign;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Campaign;

internal class CampaignCalendarDataMessageComposerSerializer(int header)
    : AbstractSerializer<CampaignCalendarDataMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CampaignCalendarDataMessageComposer message
    )
    {
        //
    }
}
