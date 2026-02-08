using Turbo.Primitives.Messages.Outgoing.Room.Layout;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Layout;

internal class RoomOccupiedTilesMessageComposerSerializer(int header)
    : AbstractSerializer<RoomOccupiedTilesMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        RoomOccupiedTilesMessageComposer message
    )
    {
        //
    }
}
