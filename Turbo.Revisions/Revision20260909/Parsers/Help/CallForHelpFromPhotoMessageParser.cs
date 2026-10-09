using Turbo.Primitives.Messages.Incoming.Help;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Help;

/// <summary>
/// <c>CallForHelpFromPhotoMessageComposer</c>: the photo's extra data id, room id, reported user
/// id, topic id, the photo's wall item id, then the name and email. No message, no chat.
/// </summary>
internal class CallForHelpFromPhotoMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var extraDataId = packet.PopString();
        var roomId = packet.PopInt();
        var reportedPlayerId = packet.PopInt();
        var topicId = packet.PopInt();
        var itemId = packet.PopInt();

        return new CallForHelpFromPhotoMessage
        {
            Submission = new CfhSubmissionSnapshot
            {
                Source = CfhSourceType.Photo,
                Message = string.Empty,
                TopicId = topicId,
                ReportedPlayerId = reportedPlayerId,
                RoomId = roomId,
                ChatLines = [],
                ExtraDataId = extraDataId,
                ItemId = itemId,
                Name = packet.PopString(),
                Email = packet.PopString(),
            },
        };
    }
}
