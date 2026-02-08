using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.FriendList;

internal class MessengerInitMessageComposerSerializer(int header)
    : AbstractSerializer<MessengerInitMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, MessengerInitMessageComposer message)
    {
        //
    }
}
