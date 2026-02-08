using Turbo.Primitives.Messages.Outgoing.Availability;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Availability;

internal class InfoHotelClosedMessageComposerSerializer(int header)
    : AbstractSerializer<InfoHotelClosedMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, InfoHotelClosedMessageComposer message)
    {
        //
    }
}
