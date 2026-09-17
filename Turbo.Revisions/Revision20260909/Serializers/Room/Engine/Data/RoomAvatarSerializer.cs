using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Engine.Data;

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

        switch (item)
        {
            case RoomPlayerAvatarSnapshot player:
                SerializePlayerAvatar(packet, player);
                break;
            case RoomPetAvatarSnapshot pet:
                SerializePetAvatar(packet, pet);
                break;
            case RoomRentableBotAvatarSnapshot bot:
                SerializeRentableBotAvatar(packet, bot);
                break;
        }
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
            .WriteBoolean(snapshot.IsModerator)
            .WriteInteger(snapshot.BadgesRank);
    }

    public static void SerializePetAvatar(IServerPacket packet, RoomPetAvatarSnapshot snapshot)
    {
        packet
            .WriteInteger(snapshot.SubType)
            .WriteInteger(snapshot.OwnerId)
            .WriteString(snapshot.OwnerName)
            .WriteInteger(snapshot.RarityLevel)
            .WriteBoolean(snapshot.HasSaddle)
            .WriteBoolean(snapshot.IsRiding)
            .WriteBoolean(snapshot.CanBreed)
            .WriteBoolean(snapshot.CanHarvest)
            .WriteBoolean(snapshot.CanRevive)
            .WriteBoolean(snapshot.HasBreedingPermission)
            .WriteInteger(snapshot.PetLevel)
            .WriteString(snapshot.PetPosture);
    }

    public static void SerializeRentableBotAvatar(
        IServerPacket packet,
        RoomRentableBotAvatarSnapshot snapshot
    )
    {
        packet
            .WriteString(AvatarGenderTypeExtensions.ToLegacyString(snapshot.Gender))
            .WriteInteger(snapshot.OwnerId)
            .WriteString(snapshot.OwnerName)
            .WriteInteger(snapshot.BotSkills.Length);

        foreach (var skill in snapshot.BotSkills)
            packet.WriteShort(skill);
    }
}
