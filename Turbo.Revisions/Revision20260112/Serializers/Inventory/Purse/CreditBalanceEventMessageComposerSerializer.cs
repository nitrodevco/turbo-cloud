using Turbo.Primitives.Messages.Outgoing.Inventory.Purse;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Purse;

internal class CreditBalanceEventMessageComposerSerializer(int header)
    : AbstractSerializer<CreditBalanceEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CreditBalanceEventMessageComposer message
    )
    {
        packet.WriteString(message.Balance);
    }
}
