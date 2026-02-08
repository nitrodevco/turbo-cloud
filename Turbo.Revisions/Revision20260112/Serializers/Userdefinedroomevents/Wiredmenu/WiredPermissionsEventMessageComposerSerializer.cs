using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Userdefinedroomevents.Wiredmenu;

internal class WiredPermissionsEventMessageComposerSerializer(int header)
    : AbstractSerializer<WiredPermissionsEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredPermissionsEventMessageComposer message
    )
    {
        packet.WriteBoolean(message.CanModify).WriteBoolean(message.CanRead);
    }
}
