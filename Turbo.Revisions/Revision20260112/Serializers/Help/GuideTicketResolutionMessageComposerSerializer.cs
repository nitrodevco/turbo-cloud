using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Help;

internal class GuideTicketResolutionMessageComposerSerializer(int header)
    : AbstractSerializer<GuideTicketResolutionMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GuideTicketResolutionMessageComposer message
    )
    {
        //
    }
}
