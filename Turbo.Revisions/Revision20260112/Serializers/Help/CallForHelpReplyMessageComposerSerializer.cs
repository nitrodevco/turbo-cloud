using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Help;

internal class CallForHelpReplyMessageComposerSerializer(int header)
    : AbstractSerializer<CallForHelpReplyMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, CallForHelpReplyMessageComposer message)
    {
        //
    }
}
