using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Catalog;

internal class SeasonalCalendarDailyOfferMessageComposerSerializer(int header)
    : AbstractSerializer<SeasonalCalendarDailyOfferMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        SeasonalCalendarDailyOfferMessageComposer message
    )
    {
        //
    }
}
