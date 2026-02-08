using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Notifications;

internal class ElementPointerMessageComposerSerializer(int header)
    : AbstractSerializer<ElementPointerMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ElementPointerMessageComposer message)
    {
        //
    }
}
