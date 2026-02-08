using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.FriendList;

internal class NewConsoleMessageMessageSerializer(int header)
    : AbstractSerializer<NewConsoleMessageMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        NewConsoleMessageMessageComposer message
    )
    {
        packet.WriteInteger(message.ChatId);
        packet.WriteString(message.Message);
        packet.WriteInteger(message.SecondsSinceSent);
        packet.WriteString(message.MessageId);
        packet.WriteInteger(message.ConfirmationId);
        packet.WriteInteger(message.SenderId);
        packet.WriteString(message.SenderName);
        packet.WriteString(message.SenderFigure);
    }
}
