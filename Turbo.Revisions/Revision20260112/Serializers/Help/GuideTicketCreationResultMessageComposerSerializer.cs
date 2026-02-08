using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Help;

internal class GuideTicketCreationResultMessageComposerSerializer(int header)
    : AbstractSerializer<GuideTicketCreationResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GuideTicketCreationResultMessageComposer message
    )
    {
        //
    }
}
