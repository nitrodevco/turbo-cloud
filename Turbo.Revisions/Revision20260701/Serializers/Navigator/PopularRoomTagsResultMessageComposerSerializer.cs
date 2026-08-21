using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Navigator;

internal class PopularRoomTagsResultMessageComposerSerializer(int header)
    : AbstractSerializer<PopularRoomTagsResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        PopularRoomTagsResultMessageComposer message
    )
    {
        //
    }
}
