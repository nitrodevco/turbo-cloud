using Turbo.Primitives.Orleans.Snapshots.Room.Furniture;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine.Data;

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
