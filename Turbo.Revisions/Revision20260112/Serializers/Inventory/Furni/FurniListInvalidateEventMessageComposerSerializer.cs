using Turbo.Primitives.Messages.Outgoing.Inventory.Furni;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Furni;

internal class FurniListInvalidateEventMessageComposerSerializer(int header)
    : AbstractSerializer<FurniListInvalidateEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        FurniListInvalidateEventMessageComposer message
    )
    {
        //
    }
}
