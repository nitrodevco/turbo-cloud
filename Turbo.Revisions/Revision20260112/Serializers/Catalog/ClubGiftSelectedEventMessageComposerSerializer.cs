using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Catalog;

internal class ClubGiftSelectedEventMessageComposerSerializer(int header)
    : AbstractSerializer<ClubGiftSelectedEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ClubGiftSelectedEventMessageComposer message
    )
    {
        //
    }
}
