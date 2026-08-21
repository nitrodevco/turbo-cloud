using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Revisions.Revision20260701.Serializers.Room.Engine.Data;

internal class AreaHideDataSerializer
{
    public static void Serialize(IServerPacket packet, AreaHideDataSnapshot areaHideData)
    {
        packet
            .WriteInteger(areaHideData.FurniId)
            .WriteBoolean(areaHideData.On)
            .WriteInteger(areaHideData.RootX)
            .WriteInteger(areaHideData.RootY)
            .WriteInteger(areaHideData.Width)
            .WriteInteger(areaHideData.Length)
            .WriteBoolean(areaHideData.Invert);
    }
}
