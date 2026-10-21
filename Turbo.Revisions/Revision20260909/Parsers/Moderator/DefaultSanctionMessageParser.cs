using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Moderator;

internal class DefaultSanctionMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var userId = packet.PopInt();
        var topicId = packet.PopInt();
        var message = packet.PopString();
        return new DefaultSanctionMessage
        {
            UserId = userId,
            TopicId = topicId,
            Message = message,
            IssueId = packet.End ? -1 : packet.PopInt(),
        };
    }
}
