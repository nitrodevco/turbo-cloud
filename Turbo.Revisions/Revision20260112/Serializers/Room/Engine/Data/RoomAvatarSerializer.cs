using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine.Data;

internal class RoomAvatarSerializer
{
    public static void Serialize(IServerPacket packet, RoomAvatarSnapshot item)
    {
        packet
            .WriteInteger(item.WebId)
            .WriteString(item.Name)
            .WriteString(item.Motto)
            .WriteString(item.Figure)
            .WriteInteger(item.ObjectId)
            .WriteInteger(item.X)
            .WriteInteger(item.Y)
            .WriteString(item.Z.ToString())
            .WriteInteger((int)item.BodyRotation)
            .WriteInteger((int)item.AvatarType);

        if (item is RoomPlayerAvatarSnapshot player)
            SerializePlayerAvatar(packet, player);
    }

    public static void SerializePlayerAvatar(
        IServerPacket packet,
        RoomPlayerAvatarSnapshot snapshot
    )
    {
        packet
            .WriteString(AvatarGenderTypeExtensions.ToLegacyString(snapshot.Gender))
            .WriteInteger(snapshot.GroupId)
            .WriteInteger(snapshot.GroupStatus)
            .WriteString(snapshot.GroupName)
            .WriteString(snapshot.SwimFigure)
            .WriteInteger(snapshot.ActivityPoints)
            .WriteBoolean(snapshot.IsModerator);
    }
}
