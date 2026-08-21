using Turbo.Primitives.Messages.Outgoing.Availability;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Availability;

internal class LoginFailedHotelClosedMessageComposerSerializer(int header)
    : AbstractSerializer<LoginFailedHotelClosedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        LoginFailedHotelClosedMessageComposer message
    )
    {
        //
    }
}
