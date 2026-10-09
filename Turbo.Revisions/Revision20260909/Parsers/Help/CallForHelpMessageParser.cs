using System.Collections.Immutable;
using Turbo.Primitives.Messages.Incoming.Help;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Help;

/// <summary>
/// <c>CallForHelpMessageComposer</c>: message, topic id, reported user id, room id, a count of
/// chat lines, each a user id and its text, then the name and email.
/// </summary>
internal class CallForHelpMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var message = packet.PopString();
        var topicId = packet.PopInt();
        var reportedPlayerId = packet.PopInt();
        var roomId = packet.PopInt();
        var count = packet.PopInt();
        var lines = ImmutableArray.CreateBuilder<CfhChatLineSnapshot>();

        // The count is the client's: a line is read only while the packet still holds one.
        for (var i = 0; i < count && packet.Remaining > 0; i++)
            lines.Add(
                new CfhChatLineSnapshot { PlayerId = packet.PopInt(), Text = packet.PopString() }
            );

        return new CallForHelpMessage
        {
            Submission = new CfhSubmissionSnapshot
            {
                Message = message,
                TopicId = topicId,
                ReportedPlayerId = reportedPlayerId,
                RoomId = roomId,
                ChatLines = lines.ToImmutable(),
                Name = packet.PopString(),
                Email = packet.PopString(),
            },
        };
    }
}
