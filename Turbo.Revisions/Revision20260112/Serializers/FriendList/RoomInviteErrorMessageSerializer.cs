using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.FriendList;

internal class RoomInviteErrorMessageSerializer(int header)
    : AbstractSerializer<RoomInviteErrorMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, RoomInviteErrorMessageComposer message)
    {
        packet.WriteInteger(message.ErrorCode);

        if (message.ErrorCode is 1)
        {
            packet.WriteInteger(message.FailedRecipients?.Count ?? 0);

            if (message.FailedRecipients is not null)
            {
                foreach (var recipient in message.FailedRecipients)
                {
                    packet.WriteInteger(recipient);
                }
            }
        }
    }
}
