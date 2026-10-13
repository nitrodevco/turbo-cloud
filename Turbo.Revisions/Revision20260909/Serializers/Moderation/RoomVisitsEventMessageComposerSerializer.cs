using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Moderation;

internal class RoomVisitsEventMessageComposerSerializer(int header)
    : AbstractSerializer<RoomVisitsEventMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, RoomVisitsEventMessageComposer message)
    {
        packet.WriteInteger(message.UserId).WriteString(message.UserName);
        packet.WriteInteger(message.Rooms.Length);

        foreach (var room in message.Rooms)
            packet
                .WriteInteger(room.RoomId)
                .WriteString(room.RoomName)
                .WriteInteger(room.EnterHour)
                .WriteInteger(room.EnterMinute);
    }
}
