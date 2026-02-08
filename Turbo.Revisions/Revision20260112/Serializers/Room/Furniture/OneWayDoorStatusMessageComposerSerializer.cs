using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Furniture;

internal class OneWayDoorStatusMessageComposerSerializer(int header)
    : AbstractSerializer<OneWayDoorStatusMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, OneWayDoorStatusMessageComposer message)
    {
        packet.WriteInteger(message.FurniId).WriteBoolean(message.Status);
    }
}
