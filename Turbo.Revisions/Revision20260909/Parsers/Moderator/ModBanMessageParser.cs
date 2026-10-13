using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Moderator;

internal class ModBanMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var userId = packet.PopInt();
        var message = packet.PopString();
        var topicId = packet.PopInt();
        var sanctionTypeId = packet.PopInt();
        var avatarOnly = packet.PopBoolean();
        return new ModBanMessage
        {
            UserId = userId,
            Message = message,
            TopicId = topicId,
            SanctionTypeId = sanctionTypeId,
            AvatarOnly = avatarOnly,
            IssueId = packet.End ? -1 : packet.PopInt(),
        };
    }
}
