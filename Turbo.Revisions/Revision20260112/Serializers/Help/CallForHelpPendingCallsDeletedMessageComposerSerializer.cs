using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Help;

internal class CallForHelpPendingCallsDeletedMessageComposerSerializer(int header)
    : AbstractSerializer<CallForHelpPendingCallsDeletedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CallForHelpPendingCallsDeletedMessageComposer message
    )
    {
        //
    }
}
