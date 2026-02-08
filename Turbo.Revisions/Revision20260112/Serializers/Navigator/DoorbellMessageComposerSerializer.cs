using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Navigator;

internal class DoorbellMessageComposerSerializer(int header)
    : AbstractSerializer<DoorbellMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, DoorbellMessageComposer message)
    {
        packet.WriteString(message.Username ?? string.Empty);
    }
}
