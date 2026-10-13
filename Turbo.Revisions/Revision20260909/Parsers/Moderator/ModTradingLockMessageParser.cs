using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Moderator;

internal class ModTradingLockMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var userId = packet.PopInt();
        var message = packet.PopString();
        var minutes = packet.PopInt();
        var topicId = packet.PopInt();
        return new ModTradingLockMessage
        {
            UserId = userId,
            Message = message,
            Minutes = minutes,
            TopicId = topicId,
            IssueId = packet.End ? -1 : packet.PopInt(),
        };
    }
}
