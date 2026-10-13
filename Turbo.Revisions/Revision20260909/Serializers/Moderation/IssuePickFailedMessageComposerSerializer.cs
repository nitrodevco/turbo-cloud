using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Moderation;

internal class IssuePickFailedMessageComposerSerializer(int header)
    : AbstractSerializer<IssuePickFailedMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, IssuePickFailedMessageComposer message)
    {
        packet.WriteInteger(message.Issues.Length);

        foreach (var issue in message.Issues)
            packet
                .WriteInteger(issue.IssueId)
                .WriteInteger(issue.PickerUserId)
                .WriteString(issue.PickerUserName);

        packet.WriteBoolean(message.RetryEnabled).WriteInteger(message.RetryCount);
    }
}
