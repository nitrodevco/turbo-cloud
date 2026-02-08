using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Catalog;

internal class FigureSetIdsMessageSerializer(int header)
    : AbstractSerializer<FigureSetIdsMessage>(header)
{
    protected override void Serialize(IServerPacket packet, FigureSetIdsMessage message)
    {
        packet.WriteInteger(0); //length

        packet.WriteInteger(0); //length
    }
}
