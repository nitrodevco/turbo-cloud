using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Navigator;

internal class RoomEventCancelMessageComposerSerializer(int header)
    : AbstractSerializer<RoomEventCancelMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, RoomEventCancelMessageComposer message)
    {
        //
    }
}
