using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Help;

internal class GuideSessionRequesterRoomMessageComposerSerializer(int header)
    : AbstractSerializer<GuideSessionRequesterRoomMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GuideSessionRequesterRoomMessageComposer message
    )
    {
        //
    }
}
