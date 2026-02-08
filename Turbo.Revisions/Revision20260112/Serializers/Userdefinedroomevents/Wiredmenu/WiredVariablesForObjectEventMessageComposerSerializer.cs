using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Revisions.Revision20260112.Serializers.Userdefinedroomevents.Wiredmenu;

internal class WiredVariablesForObjectEventMessageComposerSerializer(int header)
    : AbstractSerializer<WiredVariablesForObjectEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredVariablesForObjectEventMessageComposer message
    )
    {
        packet
            .WriteInteger((int)message.TargetType)
            .WriteInteger(message.TargetId)
            .WriteInteger(message.VariableValues.Count);

        foreach (var (id, value) in message.VariableValues)
            packet.WriteString(id.ToString()).WriteInteger(value);

        if (message.TargetType == WiredVariableTargetType.Furni)
        {
            packet.WriteInteger(message.ConfiguredInWireds.Count);

            foreach (var id in message.ConfiguredInWireds)
                packet.WriteInteger(id);
        }
    }
}
