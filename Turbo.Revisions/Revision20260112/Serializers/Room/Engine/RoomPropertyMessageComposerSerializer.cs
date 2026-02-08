using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine;

internal class RoomPropertyMessageComposerSerializer(int header)
    : AbstractSerializer<RoomPropertyMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, RoomPropertyMessageComposer message)
    {
        //
    }
}
