using Turbo.Primitives.Messages.Outgoing.Inventory.Pets;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Pets;

internal class PetRetirementPriceMessageComposerSerializer(int header)
    : AbstractSerializer<PetRetirementPriceMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        PetRetirementPriceMessageComposer message
    )
    {
        packet.WriteInteger(message.Credits);
    }
}
