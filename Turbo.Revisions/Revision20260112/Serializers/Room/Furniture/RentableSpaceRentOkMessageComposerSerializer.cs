using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Furniture;

internal class RentableSpaceRentOkMessageComposerSerializer(int header)
    : AbstractSerializer<RentableSpaceRentOkMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        RentableSpaceRentOkMessageComposer message
    )
    {
        packet.WriteInteger(message.ExpiryTime);
    }
}
