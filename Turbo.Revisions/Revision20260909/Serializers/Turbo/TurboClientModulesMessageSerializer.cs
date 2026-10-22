using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Turbo;

internal class TurboClientModulesMessageSerializer(int header)
    : AbstractSerializer<TurboClientModulesMessage>(header)
{
    protected override void Serialize(IServerPacket packet, TurboClientModulesMessage message)
    {
        packet.WriteInteger(message.Modules.Length);

        foreach (var module in message.Modules)
        {
            packet.WriteString(module.Key);
            packet.WriteString(module.Url);
            packet.WriteString(module.Version);
            packet.WriteString(module.Sha256);
        }
    }
}
