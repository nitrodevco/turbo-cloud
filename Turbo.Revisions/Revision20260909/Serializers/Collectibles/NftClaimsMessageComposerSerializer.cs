using Turbo.Primitives.Messages.Outgoing.Collectibles;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Collectibles;

internal class NftClaimsMessageComposerSerializer(int header)
    : AbstractSerializer<NftClaimsMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, NftClaimsMessageComposer message)
    {
        packet.WriteInteger(0);
    }
}
