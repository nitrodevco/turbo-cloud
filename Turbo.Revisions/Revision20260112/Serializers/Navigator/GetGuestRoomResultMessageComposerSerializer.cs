using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260112.Serializers.Navigator.Data;

namespace Turbo.Revisions.Revision20260112.Serializers.Navigator;

internal class GetGuestRoomResultMessageComposerSerializer(int header)
    : AbstractSerializer<GetGuestRoomResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GetGuestRoomResultMessageComposer message
    )
    {
        packet.WriteBoolean(message.EnterRoom);

        RoomSettingsSerializer.Serialize(packet, message.RoomInfo);

        packet
            .WriteBoolean(message.RoomForward)
            .WriteBoolean(message.StaffPick)
            .WriteBoolean(message.IsGroupMember)
            .WriteBoolean(message.AllInRoomMuted);

        ModSettingsSnapshotSerializer.Serialize(packet, message.RoomInfo.ModSettings);

        packet.WriteBoolean(message.CanMute);

        RoomChatSettingsSerializer.Serialize(packet, message.RoomInfo.ChatSettings);
    }
}
