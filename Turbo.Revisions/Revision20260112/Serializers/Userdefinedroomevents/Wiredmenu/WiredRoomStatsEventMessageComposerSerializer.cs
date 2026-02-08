using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Userdefinedroomevents.Wiredmenu;

internal class WiredRoomStatsEventMessageComposerSerializer(int header)
    : AbstractSerializer<WiredRoomStatsEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredRoomStatsEventMessageComposer message
    )
    {
        //
    }
}
