using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Revisions.Revision20260112.Serializers.Navigator.Data;

internal class RoomSettingsSerializer
{
    public static void Serialize(IServerPacket packet, RoomInfoSnapshot message)
    {
        packet
            .WriteInteger(message.RoomId)
            .WriteString(message.Name)
            .WriteInteger(message.OwnerId)
            .WriteString(message.OwnerName)
            .WriteInteger((int)message.DoorMode)
            .WriteInteger(message.Population)
            .WriteInteger(message.PlayersMax)
            .WriteString(message.Description)
            .WriteInteger((int)message.TradeType)
            .WriteInteger(message.Score)
            .WriteInteger(message.Ranking)
            .WriteInteger(message.CategoryId)
            .WriteInteger(message.Tags.Length);

        foreach (var tag in message.Tags)
            packet.WriteString(tag);

        var bitmask = RoomBitmaskFlags.None;

        bitmask |= RoomBitmaskFlags.ShowOwner;

        packet.WriteInteger((int)bitmask);

        if (bitmask.HasFlag(RoomBitmaskFlags.Thumbnail))
        {
            packet.WriteString(string.Empty); // officialRoomPicRef
        }

        if (bitmask.HasFlag(RoomBitmaskFlags.GroupData))
        {
            packet
                .WriteInteger(0) // groupId
                .WriteString(string.Empty) // groupName
                .WriteString(string.Empty); // groupBadgeCode
        }

        if (bitmask.HasFlag(RoomBitmaskFlags.RoomAd))
        {
            packet
                .WriteString(string.Empty) // adName
                .WriteString(string.Empty) // adDescription
                .WriteInteger(0); // roomAdExpiresInMin
        }
    }
}
