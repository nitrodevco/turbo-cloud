using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Furniture;

internal class RentableSpaceRentFailedMessageComposerSerializer(int header)
    : AbstractSerializer<RentableSpaceRentFailedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        RentableSpaceRentFailedMessageComposer message
    )
    {
        packet.WriteInteger((int)message.Reason);
    }
}
