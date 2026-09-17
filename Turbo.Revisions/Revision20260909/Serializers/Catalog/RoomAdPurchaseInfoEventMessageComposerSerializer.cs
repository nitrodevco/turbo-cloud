using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Catalog;

internal class RoomAdPurchaseInfoEventMessageComposerSerializer(int header)
    : AbstractSerializer<RoomAdPurchaseInfoEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        RoomAdPurchaseInfoEventMessageComposer message
    )
    {
        packet.WriteBoolean(message.IsVip).WriteInteger(message.Rooms.Length);

        foreach (var room in message.Rooms)
        {
            packet
                .WriteInteger(room.RoomId)
                .WriteString(room.Name)
                .WriteBoolean(room.HasControllers);
        }
    }
}
