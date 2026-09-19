using Turbo.Primitives.Messages.Outgoing.Room.Pets;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Pets.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Pets;

internal class PetFigureUpdateMessageComposerSerializer(int header)
    : AbstractSerializer<PetFigureUpdateMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, PetFigureUpdateMessageComposer message)
    {
        packet.WriteInteger(message.ObjectId).WriteInteger(message.PetId);

        PetFigureSerializer.Serialize(packet, message.Figure);

        packet.WriteBoolean(message.HasSaddle).WriteBoolean(message.IsRiding);
    }
}
