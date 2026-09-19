using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

internal class VariableFxConfigsRemovedMessageComposerSerializer(int header)
    : AbstractSerializer<VariableFxConfigsRemovedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        VariableFxConfigsRemovedMessageComposer message
    )
    {
        packet.WriteInteger(message.ConfigIds.Length);

        foreach (var configId in message.ConfigIds)
            packet.WriteInteger(configId);
    }
}
