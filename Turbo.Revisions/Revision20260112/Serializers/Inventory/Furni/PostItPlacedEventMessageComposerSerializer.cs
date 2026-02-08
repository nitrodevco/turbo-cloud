using Turbo.Primitives.Messages.Outgoing.Inventory.Furni;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Furni;

internal class PostItPlacedEventMessageComposerSerializer(int header)
    : AbstractSerializer<PostItPlacedEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        PostItPlacedEventMessageComposer message
    )
    {
        //
    }
}
