using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Notifications;

internal class RestoreClientMessageComposerSerializer(int header)
    : AbstractSerializer<RestoreClientMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, RestoreClientMessageComposer message)
    {
        //
    }
}
