using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260112.Serializers.Userdefinedroomevents.Data;

namespace Turbo.Revisions.Revision20260112.Serializers.Userdefinedroomevents.Wiredmenu;

internal class WiredAllVariableHoldersEventMessageComposerSerializer(int header)
    : AbstractSerializer<WiredAllVariableHoldersEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredAllVariableHoldersEventMessageComposer message
    )
    {
        packet.WriteInteger(0);

        WiredVariableSerializer.Serialize(packet, message.VariableSnapshot);

        packet.WriteInteger(message.ObjectValues.Count);

        foreach (var (objectId, value) in message.ObjectValues)
            packet.WriteInteger(objectId).WriteInteger(value);
    }
}
