using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.FriendList;

internal class MessengerErrorMessageSerializer(int header)
    : AbstractSerializer<MessengerErrorMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, MessengerErrorMessageComposer message)
    {
        packet.WriteInteger(message.ClientMessageId);
        packet.WriteInteger((int)message.ErrorCode);
    }
}
