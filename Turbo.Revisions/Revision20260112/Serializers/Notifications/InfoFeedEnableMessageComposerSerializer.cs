using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Notifications;

internal class InfoFeedEnableMessageComposerSerializer(int header)
    : AbstractSerializer<InfoFeedEnableMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, InfoFeedEnableMessageComposer message)
    {
        packet.WriteBoolean(message.Enabled);
    }
}
