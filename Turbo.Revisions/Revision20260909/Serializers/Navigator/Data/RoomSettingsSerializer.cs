using System;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Revisions.Revision20260909.Serializers.Navigator.Data;

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

        var now = DateTime.UtcNow;
        var activeEvent = message.ActiveEvent is { } evt && evt.IsActiveAt(now) ? evt : null;
        var bitmask = RoomBitmaskFlags.ShowOwner;

        if (message.AllowPets)
            bitmask |= RoomBitmaskFlags.AllowPets;

        if (message.Guild is not null)
            bitmask |= RoomBitmaskFlags.GroupData;

        if (activeEvent is not null)
            bitmask |= RoomBitmaskFlags.RoomAd;

        packet.WriteInteger((int)bitmask);

        if (bitmask.HasFlag(RoomBitmaskFlags.Thumbnail))
        {
            packet.WriteString(string.Empty); // officialRoomPicRef
        }

        if (bitmask.HasFlag(RoomBitmaskFlags.GroupData))
        {
            packet
                .WriteInteger(message.Guild!.GuildId)
                .WriteString(message.Guild.Name)
                .WriteString(message.Guild.BadgeCode);
        }

        if (bitmask.HasFlag(RoomBitmaskFlags.RoomAd))
        {
            packet
                .WriteString(activeEvent!.Name)
                .WriteString(activeEvent.Description)
                .WriteInteger(activeEvent.MinutesUntilExpiry(now));
        }
    }
}
