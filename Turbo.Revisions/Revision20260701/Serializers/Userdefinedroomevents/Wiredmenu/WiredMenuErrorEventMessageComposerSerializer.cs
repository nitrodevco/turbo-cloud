using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Userdefinedroomevents.Wiredmenu;

internal class WiredMenuErrorEventMessageComposerSerializer(int header)
    : AbstractSerializer<WiredMenuErrorEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredMenuErrorEventMessageComposer message
    )
    {
        //
    }
}
