using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Moderation;

internal class IssuePickFailedMessageComposerSerializer(int header)
    : AbstractSerializer<IssuePickFailedMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, IssuePickFailedMessageComposer message)
    {
        //
    }
}
