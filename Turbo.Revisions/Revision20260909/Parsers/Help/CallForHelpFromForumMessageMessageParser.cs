using Turbo.Primitives.Messages.Incoming.Help;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Help;

/// <summary>
/// <c>CallForHelpFromForumMessageMessageComposer</c>: the group id, thread id, message id, topic
/// id, the message, then the name and email (TopicsFlowHelpController). The message's author is
/// who is reported.
/// </summary>
internal class CallForHelpFromForumMessageMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var groupId = packet.PopInt();
        var threadId = packet.PopInt();
        var messageId = packet.PopInt();
        var topicId = packet.PopInt();

        return new CallForHelpFromForumMessageMessage
        {
            Submission = new CfhSubmissionSnapshot
            {
                Source = CfhSourceType.ForumMessage,
                GroupId = groupId,
                ThreadId = threadId,
                MessageId = messageId,
                TopicId = topicId,
                Message = packet.PopString(),
                ReportedPlayerId = 0,
                RoomId = 0,
                ChatLines = [],
                Name = packet.PopString(),
                Email = packet.PopString(),
            },
        };
    }
}
