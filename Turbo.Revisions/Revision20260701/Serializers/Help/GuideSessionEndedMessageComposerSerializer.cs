using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Help;

internal class GuideSessionEndedMessageComposerSerializer(int header)
    : AbstractSerializer<GuideSessionEndedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GuideSessionEndedMessageComposer message
    )
    {
        //
    }
}
