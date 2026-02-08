using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.FriendList;

internal class ConsoleMessageHistoryMessageSerializer(int header)
    : AbstractSerializer<ConsoleMessageHistoryMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ConsoleMessageHistoryMessageComposer message
    )
    {
        packet.WriteInteger(message.ChatId);
        packet.WriteInteger(message.Messages.Count);

        foreach (var consoleMessage in message.Messages)
        {
            packet.WriteInteger(consoleMessage.SenderId);
            packet.WriteString(consoleMessage.SenderName);
            packet.WriteString(consoleMessage.SenderFigure);
            packet.WriteString(consoleMessage.Message);
            packet.WriteInteger(consoleMessage.SecondsSinceSent);
            packet.WriteString(consoleMessage.MessageId);
        }
    }
}
