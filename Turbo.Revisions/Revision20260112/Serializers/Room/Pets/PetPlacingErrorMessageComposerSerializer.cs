using Turbo.Primitives.Messages.Outgoing.Room.Pets;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Pets;

internal class PetPlacingErrorMessageComposerSerializer(int header)
    : AbstractSerializer<PetPlacingErrorMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, PetPlacingErrorMessageComposer message)
    {
        //
    }
}
