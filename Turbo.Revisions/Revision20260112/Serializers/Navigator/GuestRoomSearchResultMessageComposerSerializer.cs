using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Navigator;

internal class GuestRoomSearchResultMessageComposerSerializer(int header)
    : AbstractSerializer<GuestRoomSearchResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GuestRoomSearchResultMessageComposer message
    )
    {
        //
    }
}
