using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Moderator;

internal class ModMuteMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var userId = packet.PopInt();
        var message = packet.PopString();
        var topicId = packet.PopInt();
        return new ModMuteMessage
        {
            UserId = userId,
            Message = message,
            TopicId = topicId,
            IssueId = packet.End ? -1 : packet.PopInt(),
        };
    }
}
