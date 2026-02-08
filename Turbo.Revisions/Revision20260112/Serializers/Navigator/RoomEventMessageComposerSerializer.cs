using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Navigator;

internal class RoomEventMessageComposerSerializer(int header)
    : AbstractSerializer<RoomEventMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, RoomEventMessageComposer message)
    {
        //
    }
}
