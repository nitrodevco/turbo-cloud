using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Catalog;

internal class BuildersClubFurniCountMessageComposerSerializer(int header)
    : AbstractSerializer<BuildersClubFurniCountMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        BuildersClubFurniCountMessageComposer message
    ) => packet.WriteInteger(message.FurniCount);
}
