using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Help;

internal class CallForHelpResultMessageComposerSerializer(int header)
    : AbstractSerializer<CallForHelpResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CallForHelpResultMessageComposer message
    )
    {
        //
    }
}
