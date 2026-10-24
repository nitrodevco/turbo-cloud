using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Turbo;

internal class TurboPluginMessagesMessageSerializer(int header)
    : AbstractSerializer<TurboPluginMessagesMessage>(header)
{
    protected override void Serialize(IServerPacket packet, TurboPluginMessagesMessage message)
    {
        packet.WriteInteger(message.Messages.Length);

        foreach (var entry in message.Messages)
        {
            packet.WriteString(entry.Name);
            packet.WriteInteger(entry.Header);
            packet.WriteString(entry.Schema);
        }
    }
}
