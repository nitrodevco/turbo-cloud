using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Revisions.Revision20260112.Serializers.Room.Engine.Data;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine;

internal class FloorHeightMapMessageComposerSerializer(int header)
    : AbstractSerializer<FloorHeightMapMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, FloorHeightMapMessageComposer message)
    {
        packet
            .WriteBoolean(message.ScaleType is RoomScaleType.Small)
            .WriteInteger(message.FixedWallsHeight)
            .WriteString(message.ModelData)
            .WriteInteger(message.AreaHideData.Count);

        foreach (var area in message.AreaHideData)
            AreaHideDataSerializer.Serialize(packet, area);
    }
}
