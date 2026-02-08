using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Help;

internal class GuideSessionStartedMessageComposerSerializer(int header)
    : AbstractSerializer<GuideSessionStartedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GuideSessionStartedMessageComposer message
    )
    {
        //
    }
}
