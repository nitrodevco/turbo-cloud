using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Action;

internal class CarryObjectMessageComposerSerializer(int header)
    : AbstractSerializer<CarryObjectMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, CarryObjectMessageComposer message)
    {
        packet.WriteInteger(message.UserId).WriteInteger(message.ItemType);
    }
}
