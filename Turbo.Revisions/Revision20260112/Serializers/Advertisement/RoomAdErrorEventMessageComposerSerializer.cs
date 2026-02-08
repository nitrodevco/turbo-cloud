using Turbo.Primitives.Messages.Outgoing.Advertisement;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Advertisement;

internal class RoomAdErrorEventMessageComposerSerializer(int header)
    : AbstractSerializer<RoomAdErrorEventMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, RoomAdErrorEventMessageComposer message)
    {
        //
    }
}
