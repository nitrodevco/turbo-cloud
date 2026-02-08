using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Help;

internal class GuideSessionMessageMessageComposerSerializer(int header)
    : AbstractSerializer<GuideSessionMessageMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GuideSessionMessageMessageComposer message
    )
    {
        //
    }
}
