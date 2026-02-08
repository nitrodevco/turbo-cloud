using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Roomsettings;

internal class UserUnbannedFromRoomEventMessageComposerSerializer(int header)
    : AbstractSerializer<UserUnbannedFromRoomEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        UserUnbannedFromRoomEventMessageComposer message
    )
    {
        //
    }
}
