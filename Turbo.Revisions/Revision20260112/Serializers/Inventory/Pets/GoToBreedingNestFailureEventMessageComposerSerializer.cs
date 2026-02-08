using Turbo.Primitives.Messages.Outgoing.Inventory.Pets;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Pets;

internal class GoToBreedingNestFailureEventMessageComposerSerializer(int header)
    : AbstractSerializer<GoToBreedingNestFailureEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GoToBreedingNestFailureEventMessageComposer message
    )
    {
        //
    }
}
