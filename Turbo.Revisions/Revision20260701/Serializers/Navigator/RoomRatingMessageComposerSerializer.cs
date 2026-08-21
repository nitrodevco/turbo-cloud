using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Navigator;

internal class RoomRatingMessageComposerSerializer(int header)
    : AbstractSerializer<RoomRatingMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, RoomRatingMessageComposer message)
    {
        packet.WriteInteger(message.Rating).WriteBoolean(message.CanRate);
    }
}
