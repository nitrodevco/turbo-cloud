using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Moderation;

internal class ModeratorRoomInfoEventMessageComposerSerializer(int header)
    : AbstractSerializer<ModeratorRoomInfoEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ModeratorRoomInfoEventMessageComposer message
    )
    {
        var room = message.Room;

        packet
            .WriteInteger(room.RoomId)
            .WriteInteger(room.UserCount)
            .WriteBoolean(room.OwnerInRoom)
            .WriteInteger(room.OwnerId)
            .WriteString(room.OwnerName)
            .WriteBoolean(room.Exists);

        if (!room.Exists)
            return;

        packet.WriteString(room.Name).WriteString(room.Description).WriteInteger(room.Tags.Length);

        foreach (var tag in room.Tags)
            packet.WriteString(tag);
    }
}
