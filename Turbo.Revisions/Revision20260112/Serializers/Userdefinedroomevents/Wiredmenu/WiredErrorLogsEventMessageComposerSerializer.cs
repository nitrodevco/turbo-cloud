using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Userdefinedroomevents.Wiredmenu;

internal class WiredErrorLogsEventMessageComposerSerializer(int header)
    : AbstractSerializer<WiredErrorLogsEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredErrorLogsEventMessageComposer message
    )
    {
        //
    }
}
