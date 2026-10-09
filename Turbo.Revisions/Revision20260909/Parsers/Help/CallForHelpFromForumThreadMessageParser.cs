using Turbo.Primitives.Messages.Incoming.Help;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Help;

/// <summary>
/// <c>CallForHelpFromForumThreadMessageComposer</c>: the group id, thread id, topic id, the
/// message, then the name and email (TopicsFlowHelpController). No one reported, no chat: the
/// thread's author is who is reported.
/// </summary>
internal class CallForHelpFromForumThreadMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var groupId = packet.PopInt();
        var threadId = packet.PopInt();
        var topicId = packet.PopInt();

        return new CallForHelpFromForumThreadMessage
        {
            Submission = new CfhSubmissionSnapshot
            {
                Source = CfhSourceType.ForumThread,
                GroupId = groupId,
                ThreadId = threadId,
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
