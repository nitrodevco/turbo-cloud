using Turbo.Primitives.Messages.Incoming.Help;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Help;

/// <summary>
/// <c>CallForHelpMessageComposer</c>: message, topic id, reported user id, room id, the chat
/// lines, then the name and email.
/// </summary>
internal class CallForHelpMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new CallForHelpMessage
        {
            Submission = new CfhSubmissionSnapshot
            {
                Message = packet.PopString(),
                TopicId = packet.PopInt(),
                ReportedPlayerId = packet.PopInt(),
                RoomId = packet.PopInt(),
                ChatLines = CfhChatLinesParser.Parse(packet),
                Name = packet.PopString(),
                Email = packet.PopString(),
            },
        };
}
