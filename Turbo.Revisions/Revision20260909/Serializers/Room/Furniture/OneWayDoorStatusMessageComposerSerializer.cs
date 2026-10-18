using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Furniture;

internal class OneWayDoorStatusMessageComposerSerializer(int header)
    : AbstractSerializer<OneWayDoorStatusMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, OneWayDoorStatusMessageComposer message)
    {
        // Habbo sends a third int, always 0 (official client capture 2026-10-17, 49 of 49); its
        // client reads only the first two.
        packet.WriteInteger(message.FurniId).WriteInteger(message.Status).WriteInteger(0);
    }
}
