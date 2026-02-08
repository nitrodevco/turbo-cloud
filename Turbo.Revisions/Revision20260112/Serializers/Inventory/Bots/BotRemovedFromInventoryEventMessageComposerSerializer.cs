using Turbo.Primitives.Messages.Outgoing.Inventory.Bots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Bots;

internal class BotRemovedFromInventoryEventMessageComposerSerializer(int header)
    : AbstractSerializer<BotRemovedFromInventoryEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        BotRemovedFromInventoryEventMessageComposer message
    )
    {
        //
    }
}
