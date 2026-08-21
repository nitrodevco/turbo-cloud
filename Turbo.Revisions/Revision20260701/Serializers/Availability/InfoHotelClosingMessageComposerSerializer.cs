using Turbo.Primitives.Messages.Outgoing.Availability;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Availability;

internal class InfoHotelClosingMessageComposerSerializer(int header)
    : AbstractSerializer<InfoHotelClosingMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, InfoHotelClosingMessageComposer message)
    {
        //
    }
}
