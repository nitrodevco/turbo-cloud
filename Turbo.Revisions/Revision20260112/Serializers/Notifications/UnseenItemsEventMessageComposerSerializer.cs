using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Notifications;

internal class UnseenItemsEventMessageComposerSerializer(int header)
    : AbstractSerializer<UnseenItemsEventMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, UnseenItemsEventMessageComposer message)
    {
        //
    }
}
