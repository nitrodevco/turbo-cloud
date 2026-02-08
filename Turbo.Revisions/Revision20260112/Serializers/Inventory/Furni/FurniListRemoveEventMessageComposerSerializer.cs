using Turbo.Primitives.Messages.Outgoing.Inventory.Furni;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Furni;

internal class FurniListRemoveEventMessageComposerSerializer(int header)
    : AbstractSerializer<FurniListRemoveEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        FurniListRemoveEventMessageComposer message
    )
    {
        packet.WriteInteger(message.ItemId);
    }
}
