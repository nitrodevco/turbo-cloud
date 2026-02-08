using Turbo.Primitives.Messages.Outgoing.Inventory.Bots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Bots;

internal class BotAddedToInventoryEventMessageComposerSerializer(int header)
    : AbstractSerializer<BotAddedToInventoryEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        BotAddedToInventoryEventMessageComposer message
    )
    {
        //
    }
}
