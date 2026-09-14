using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260701.Serializers.Navigator.Data;

namespace Turbo.Revisions.Revision20260701.Serializers.Roomsettings;

internal class RoomSettingsDataEventMessageComposerSerializer(int header)
    : AbstractSerializer<RoomSettingsDataEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        RoomSettingsDataEventMessageComposer message
    )
    {
        packet
            .WriteInteger(message.RoomId)
            .WriteString(message.Name)
            .WriteString(message.Description)
            .WriteInteger((int)message.DoorMode)
            .WriteInteger(message.CategoryId)
            .WriteInteger(message.MaximumVisitors)
            .WriteInteger(message.MaximumVisitorsLimit)
            .WriteInteger(message.Tags.Length);

        foreach (var tag in message.Tags)
            packet.WriteString(tag);

        packet
            .WriteInteger((int)message.TradeMode)
            .WriteInteger(message.AllowPets ? 1 : 0)
            .WriteInteger(message.AllowFoodConsume ? 1 : 0)
            .WriteInteger(message.AllowWalkThrough ? 1 : 0)
            .WriteInteger(message.HideWalls ? 1 : 0)
            .WriteInteger((int)message.WallThickness)
            .WriteInteger((int)message.FloorThickness)
            .WriteInteger((int)message.ChatProtection)
            .WriteBoolean(message.LeaveOnDoorTileEnabled)
            .WriteBoolean(message.IdleSleepEnabled)
            .WriteInteger(message.IdleSleepTimeoutSeconds)
            .WriteBoolean(message.IdleAutokickEnabled)
            .WriteInteger(message.IdleAutokickTimeoutSeconds)
            .WriteBoolean(message.MuteAllPets);

        ModSettingsSnapshotSerializer.Serialize(packet, message.ModSettings);

        packet.WriteBoolean(message.HiddenByBc);
    }
}
