using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Userdefinedroomevents.Wiredmenu;

internal class WiredAllVariablesHashEventMessageComposerSerializer(int header)
    : AbstractSerializer<WiredAllVariablesHashEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredAllVariablesHashEventMessageComposer message
    ) => packet.WriteInteger(message.AllVariablesHash.Value);
}
