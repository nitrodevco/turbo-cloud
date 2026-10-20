using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Wiredmenu;

internal class WiredSetUserPermanentVariableResultMessageComposerSerializer(int header)
    : AbstractSerializer<WiredSetUserPermanentVariableResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredSetUserPermanentVariableResultMessageComposer message
    ) => packet.WriteBoolean(message.Success);
}
