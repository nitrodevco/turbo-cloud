using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Turbo;

internal class TurboServerCapabilitiesMessageSerializer(int header)
    : AbstractSerializer<TurboServerCapabilitiesMessage>(header)
{
    protected override void Serialize(IServerPacket packet, TurboServerCapabilitiesMessage message)
    {
        packet.WriteInteger(message.Capabilities.Length);

        foreach (var capability in message.Capabilities)
        {
            packet.WriteString(capability.Name);
            packet.WriteInteger(capability.Version);
        }
    }
}
