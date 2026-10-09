using Turbo.Primitives.Messages.Incoming.Help;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Help;

/// <summary>
/// <c>CallForHelpFromIMMessageComposer</c>: message, topic id, reported user id, the
/// conversation's lines, then the name and email. No room.
/// </summary>
internal class CallForHelpFromIMMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new CallForHelpFromIMMessage
        {
            Submission = new CfhSubmissionSnapshot
            {
                Source = CfhSourceType.InstantMessage,
                Message = packet.PopString(),
                TopicId = packet.PopInt(),
                ReportedPlayerId = packet.PopInt(),
                RoomId = 0,
                ChatLines = CfhChatLinesParser.Parse(packet),
                Name = packet.PopString(),
                Email = packet.PopString(),
            },
        };
}
