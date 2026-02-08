using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.FriendList;

internal class InstantMessageErrorMessageSerializer(int header)
    : AbstractSerializer<InstantMessageErrorMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        InstantMessageErrorMessageComposer message
    )
    {
        packet.WriteInteger((int)message.ErrorCode);
        packet.WriteInteger(message.PlayerId);
        packet.WriteString(message.Message);
    }
}
