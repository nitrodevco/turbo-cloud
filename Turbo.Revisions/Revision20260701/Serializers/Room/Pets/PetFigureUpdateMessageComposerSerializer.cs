using Turbo.Primitives.Messages.Outgoing.Room.Pets;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Room.Pets;

internal class PetFigureUpdateMessageComposerSerializer(int header)
    : AbstractSerializer<PetFigureUpdateMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, PetFigureUpdateMessageComposer message)
    {
        //
    }
}
