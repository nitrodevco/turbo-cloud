using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players.Enums.Messenger;

namespace Turbo.Revisions.Revision20260909.Serializers.FriendList;

internal class RoomInviteErrorMessageSerializer(int header)
    : AbstractSerializer<RoomInviteErrorMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, RoomInviteErrorMessageComposer message)
    {
        packet.WriteInteger((int)message.ErrorCode);

        if (message.ErrorCode is RoomInviteErrorCodeType.RecipientsFailed)
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
