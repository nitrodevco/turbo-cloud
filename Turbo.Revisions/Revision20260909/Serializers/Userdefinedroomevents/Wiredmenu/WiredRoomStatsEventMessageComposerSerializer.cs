using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Wiredmenu;

internal class WiredRoomStatsEventMessageComposerSerializer(int header)
    : AbstractSerializer<WiredRoomStatsEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredRoomStatsEventMessageComposer message
    )
    {
        var stats = message.Stats;

        packet
            .WriteDouble(stats.ExecutionCost)
            .WriteDouble(stats.ExecutionCostCap)
            .WriteBoolean(stats.IsHeavy)
            .WriteInteger(stats.FloorItemCount)
            .WriteInteger(stats.FloorItemCap)
            .WriteInteger(stats.WallItemCount)
            .WriteInteger(stats.WallItemCap)
            .WriteInteger(stats.PermanentFurniVariables)
            .WriteInteger(stats.MaxPermanentFurniVariables)
            .WriteInteger(stats.PermanentUserVariables)
            .WriteInteger(stats.MaxPermanentUserVariables)
            .WriteInteger(stats.PermanentGlobalVariables)
            .WriteInteger(stats.MaxPermanentGlobalVariables);
    }
}
