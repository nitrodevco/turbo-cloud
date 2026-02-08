using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.FriendList;

internal class MiniMailNewMessageSerializer(int header)
    : AbstractSerializer<MiniMailNewMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, MiniMailNewMessageComposer message)
    {
        //
    }
}
