using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.FriendList;

internal class MiniMailNewMessageSerializer(int header)
    : AbstractSerializer<MiniMailNewMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, MiniMailNewMessageComposer message)
    {
        // The client reads nothing (MiniMailNewMessageMessageParser); Habbo still sends the
        // string "0", in every official client capture of it.
        packet.WriteString("0");
    }
}
