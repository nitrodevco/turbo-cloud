using Turbo.Primitives.Messages.Outgoing.Inventory.Pets;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Pets;

internal class ConfirmBreedingResultEventMessageComposerSerializer(int header)
    : AbstractSerializer<ConfirmBreedingResultEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ConfirmBreedingResultEventMessageComposer message
    )
    {
        //
    }
}
