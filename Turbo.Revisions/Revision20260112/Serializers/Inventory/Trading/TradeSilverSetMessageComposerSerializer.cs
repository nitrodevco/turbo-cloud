using Turbo.Primitives.Messages.Outgoing.Inventory.Trading;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Trading;

internal class TradeSilverSetMessageComposerSerializer(int header)
    : AbstractSerializer<TradeSilverSetMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, TradeSilverSetMessageComposer message)
    {
        //
    }
}
